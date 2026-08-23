using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.UseCases;
using informE.Domain.Entities;
using informE.Domain.Enums;
using NSubstitute;

namespace informE.Application.Tests;

public class RevokeSessionUseCaseTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private RevokeSessionUseCase CriarUseCase() => new(_users, _uow);

    private static Session SessaoDe(Guid userId) =>
        new("192.168.0.10", DateTimeOffset.Now.AddDays(7), "hash", userId, "Chrome — Windows 11");

    [Fact]
    public async Task Dono_deve_poder_encerrar_a_propria_sessao()
    {
        var userId = Guid.NewGuid();
        var sessao = SessaoDe(userId);
        _users.GetSessionByIdAsync(sessao.Id, Arg.Any<CancellationToken>()).Returns(sessao);

        await CriarUseCase().ExecuteAsync(sessao.Id, userId, UserRole.Viewer);

        Assert.False(sessao.IsActive);
    }

    [Fact]
    public async Task Viewer_nao_deve_encerrar_sessao_de_outro()
    {
        var sessao = SessaoDe(Guid.NewGuid());
        _users.GetSessionByIdAsync(sessao.Id, Arg.Any<CancellationToken>()).Returns(sessao);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CriarUseCase().ExecuteAsync(sessao.Id, Guid.NewGuid(), UserRole.Viewer));

        Assert.True(sessao.IsActive);
    }

    // É o que permite destravar um Admin preso no limite de 3 dispositivos.
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Privilegiado_deve_poder_encerrar_sessao_de_terceiro(UserRole papel)
    {
        var sessao = SessaoDe(Guid.NewGuid());
        _users.GetSessionByIdAsync(sessao.Id, Arg.Any<CancellationToken>()).Returns(sessao);

        await CriarUseCase().ExecuteAsync(sessao.Id, Guid.NewGuid(), papel);

        Assert.False(sessao.IsActive);
    }

    [Fact]
    public async Task Revogar_sessao_ja_revogada_nao_deve_estourar()
    {
        var userId = Guid.NewGuid();
        var sessao = SessaoDe(userId);
        sessao.Revoke();
        _users.GetSessionByIdAsync(sessao.Id, Arg.Any<CancellationToken>()).Returns(sessao);

        await CriarUseCase().ExecuteAsync(sessao.Id, userId, UserRole.Viewer);

        // Idempotente: não salva de novo, mas também não quebra o botão.
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sessao_inexistente_deve_lancar()
    {
        _users.GetSessionByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Session?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarUseCase().ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Admin));
    }
}
