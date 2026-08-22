using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Domain.Entities;
using informE.Domain.Enums;
using NSubstitute;

namespace informE.Application.Tests;

// Application é testada com as interfaces mockadas (NSubstitute) — sem banco,
// sem Argon2 real, sem JWT real. O que se testa aqui é a POLÍTICA de sessão,
// não a criptografia.
public class LoginUseCaseTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IAuditLogRepository _audit = Substitute.For<IAuditLogRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public LoginUseCaseTests()
    {
        _jwt.CreateAccessToken(Arg.Any<User>()).Returns("access-token");
        _jwt.CreateRefreshToken().Returns(("refresh-token", DateTimeOffset.Now.AddDays(7)));
        _hasher.Hash(Arg.Any<string>()).Returns("hash-fake");
    }

    private LoginUseCase CriarUseCase() => new(_users, _hasher, _jwt, _audit, _uow);

    private static LoginRequest Request() =>
        new("admin@etec.sp.gov.br", "senha", "192.168.0.10", "Chrome — Windows 11");

    private static User Usuario(UserRole role = UserRole.Admin) =>
        new("admin", "admin@etec.sp.gov.br", "hash-no-banco", role);

    [Fact]
    public async Task Email_inexistente_deve_lancar_credencial_invalida()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => CriarUseCase().ExecuteAsync(Request()));
    }

    [Fact]
    public async Task Senha_errada_deve_lancar_a_MESMA_excecao_de_email_inexistente()
    {
        // Se as duas exceções fossem diferentes, daria pra enumerar e-mails válidos.
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Usuario());
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => CriarUseCase().ExecuteAsync(Request()));
    }

    [Fact]
    public async Task Conta_desativada_nao_deve_logar_mesmo_com_senha_certa()
    {
        var user = Usuario();
        user.Deactivate();
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await Assert.ThrowsAsync<AccountDisabledException>(() => CriarUseCase().ExecuteAsync(Request()));
    }

    [Fact]
    public async Task Credencial_valida_deve_devolver_tokens_e_persistir_sessao()
    {
        var user = Usuario();
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _users.GetActiveSessionsAsync(user.Id, Arg.Any<CancellationToken>()).Returns([]);

        var resposta = await CriarUseCase().ExecuteAsync(Request());

        Assert.Equal("access-token", resposta.AccessToken);
        Assert.Equal("refresh-token", resposta.RefreshToken);
        Assert.Equal(UserRole.Admin, resposta.Role);
        await _users.Received(1).AddSessionAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_token_nunca_deve_ir_em_texto_claro_para_a_sessao()
    {
        var user = Usuario();
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _users.GetActiveSessionsAsync(user.Id, Arg.Any<CancellationToken>()).Returns([]);

        Session? persistida = null;
        await _users.AddSessionAsync(Arg.Do<Session>(s => persistida = s), Arg.Any<CancellationToken>());

        await CriarUseCase().ExecuteAsync(Request());

        Assert.NotNull(persistida);
        Assert.NotEqual("refresh-token", persistida.RefreshTokenHash);
        _hasher.Received().Hash("refresh-token");
    }

    // política §2.1 — Admin/SuperAdmin bloqueiam o 4º dispositivo.
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Privilegiado_com_3_sessoes_vigentes_deve_bloquear_o_quarto_dispositivo(UserRole role)
    {
        var user = Usuario(role);
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _users.GetActiveSessionsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([SessaoVigente(), SessaoVigente(), SessaoVigente()]);

        await Assert.ThrowsAsync<DeviceLimitReachedException>(() => CriarUseCase().ExecuteAsync(Request()));
    }

    [Fact]
    public async Task Sessao_expirada_nao_deve_contar_para_o_limite_de_dispositivos()
    {
        // A varredura de sessões ociosas é de um BackgroundService que ainda não
        // existe, então sessão vencida continua IsActive=true no banco. Se contasse,
        // o admin ficaria trancado fora depois de 3 logins antigos.
        var user = Usuario(UserRole.Admin);
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _users.GetActiveSessionsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([SessaoExpirada(), SessaoExpirada(), SessaoExpirada()]);

        var resposta = await CriarUseCase().ExecuteAsync(Request());

        Assert.Equal("access-token", resposta.AccessToken);
    }

    // política §2.2 — Viewer é sessão única: o login novo derruba a anterior.
    [Fact]
    public async Task Viewer_deve_derrubar_a_sessao_anterior()
    {
        var user = Usuario(UserRole.Viewer);
        var anterior = SessaoVigente();
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _users.GetActiveSessionsAsync(user.Id, Arg.Any<CancellationToken>()).Returns([anterior]);

        await CriarUseCase().ExecuteAsync(Request());

        Assert.False(anterior.IsActive);
    }

    [Fact]
    public async Task Viewer_nao_deve_ser_bloqueado_por_limite_de_dispositivos()
    {
        var user = Usuario(UserRole.Viewer);
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _users.GetActiveSessionsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([SessaoVigente(), SessaoVigente(), SessaoVigente(), SessaoVigente()]);

        var resposta = await CriarUseCase().ExecuteAsync(Request());

        Assert.Equal("access-token", resposta.AccessToken);
    }

    private static Session SessaoVigente() =>
        new("192.168.0.11", DateTimeOffset.Now.AddDays(7), "hash", Guid.NewGuid());

    private static Session SessaoExpirada() =>
        new("192.168.0.11", DateTimeOffset.Now.AddMinutes(-1), "hash", Guid.NewGuid());
}
