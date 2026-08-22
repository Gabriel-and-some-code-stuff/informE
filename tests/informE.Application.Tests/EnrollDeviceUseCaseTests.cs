using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Domain.Entities;
using NSubstitute;

namespace informE.Application.Tests;

public class EnrollDeviceUseCaseTests
{
    private readonly IEnrollmentTokenRepository _tokens = Substitute.For<IEnrollmentTokenRepository>();
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public EnrollDeviceUseCaseTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("$argon2id$v=19$m=65536,t=4,p=2$c2FsdA==$aGFzaA==");
    }

    private EnrollDeviceUseCase CriarUseCase() => new(_tokens, _devices, _hasher, _uow);

    private static EnrollDeviceRequest Request() =>
        new("token-abc", "PC-01", "192.168.1.10", "AA:BB:CC:DD:EE:FF", "Windows 11", "aluno", null);

    [Fact]
    public async Task Token_inexistente_deve_lancar()
    {
        _tokens.GetByTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((EnrollmentToken?)null);

        await Assert.ThrowsAsync<EnrollmentTokenInvalidException>(() => CriarUseCase().ExecuteAsync(Request()));
    }

    [Fact]
    public async Task Token_ja_usado_nao_pode_registrar_outra_maquina()
    {
        var token = new EnrollmentToken("token-abc", Guid.NewGuid());
        token.Redeem(Guid.NewGuid()); // já gastou

        _tokens.GetByTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(token);

        await Assert.ThrowsAsync<EnrollmentTokenInvalidException>(() => CriarUseCase().ExecuteAsync(Request()));
    }

    [Fact]
    public async Task Token_expirado_deve_lancar()
    {
        var token = new EnrollmentToken("token-abc", Guid.NewGuid()) { ExpiresAt = DateTimeOffset.Now.AddMinutes(-1) };

        _tokens.GetByTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(token);

        await Assert.ThrowsAsync<EnrollmentTokenInvalidException>(() => CriarUseCase().ExecuteAsync(Request()));
    }

    [Fact]
    public async Task Enroll_valido_deve_gastar_o_token_e_amarrar_no_device()
    {
        var token = new EnrollmentToken("token-abc", Guid.NewGuid());
        _tokens.GetByTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(token);

        var resposta = await CriarUseCase().ExecuteAsync(Request());

        Assert.True(token.IsUsed);
        Assert.Equal(resposta.DeviceId, token.RedeemedByDeviceId);
        await _devices.Received(1).AddAsync(Arg.Any<Device>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Chave_do_agente_deve_ser_persistida_apenas_como_hash()
    {
        var token = new EnrollmentToken("token-abc", Guid.NewGuid());
        _tokens.GetByTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(token);

        Device? persistido = null;
        await _devices.AddAsync(Arg.Do<Device>(d => persistido = d), Arg.Any<CancellationToken>());

        var resposta = await CriarUseCase().ExecuteAsync(Request());

        // A chave em claro sai na resposta (o agente guarda com DPAPI), mas o que
        // vai pro banco é só o hash Argon2id.
        Assert.False(string.IsNullOrWhiteSpace(resposta.AgentKey));
        Assert.NotNull(persistido);
        Assert.NotEqual(resposta.AgentKey, persistido.AgentKeyHash);
        _hasher.Received().Hash(resposta.AgentKey);
    }

    [Fact]
    public async Task Cada_enroll_deve_gerar_uma_chave_diferente()
    {
        _tokens.GetByTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new EnrollmentToken("token-abc", Guid.NewGuid()));

        var primeira = await CriarUseCase().ExecuteAsync(Request());
        var segunda = await CriarUseCase().ExecuteAsync(Request());

        Assert.NotEqual(primeira.AgentKey, segunda.AgentKey);
    }
}
