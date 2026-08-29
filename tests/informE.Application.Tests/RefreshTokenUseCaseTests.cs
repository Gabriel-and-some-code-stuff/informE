using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.UseCases;
using informE.Domain.Entities;
using informE.Domain.Enums;
using NSubstitute;

namespace informE.Application.Tests;

// POST /auth/refresh. Até esta PR o refresh token era emitido, hasheado e
// persistido, e nada o consumia — a sessão morria junto com o access token de
// 15 min.
public class RefreshTokenUseCaseTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private static readonly Guid SessionId = Guid.NewGuid();

    public RefreshTokenUseCaseTests()
    {
        _jwt.CreateAccessToken(Arg.Any<User>(), Arg.Any<Guid>()).Returns("access-novo");
        _jwt.CreateRefreshToken().Returns(("segredo-novo", DateTimeOffset.UtcNow.AddDays(7)));
        _hasher.Hash(Arg.Any<string>()).Returns("hash-novo");
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
    }

    private RefreshTokenUseCase CriarUseCase() => new(_users, _hasher, _jwt, _uow);

    private static Session SessaoValida() =>
        new("192.168.1.10", DateTimeOffset.UtcNow.AddDays(7), "hash-antigo", Guid.NewGuid())
        {
            Id = SessionId
        };

    private static User UsuarioAtivo() =>
        new("admin", "admin@etec.sp.gov.br", "hash", UserRole.Admin) { Id = Guid.NewGuid() };

    private void ArranjarSessao(Session sessao, User user)
    {
        _users.GetSessionByIdAsync(sessao.Id, Arg.Any<CancellationToken>()).Returns(sessao);
        _users.GetByIdAsync(sessao.UserId, Arg.Any<CancellationToken>()).Returns(user);
    }

    [Fact]
    public async Task Token_valido_deve_devolver_par_novo_e_rotacionar_o_hash()
    {
        var sessao = SessaoValida();
        ArranjarSessao(sessao, UsuarioAtivo());

        var resposta = await CriarUseCase().ExecuteAsync($"{SessionId}.segredo-antigo");

        Assert.Equal("access-novo", resposta.AccessToken);
        Assert.Equal($"{SessionId}.segredo-novo", resposta.RefreshToken);

        // Rotação: o hash guardado tem que ser o do segredo NOVO. Sem isso, o
        // token antigo continuaria valendo e o refresh não protegeria contra
        // vazamento.
        Assert.Equal("hash-novo", sessao.RefreshTokenHash);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-ponto")]
    [InlineData("nao-e-guid.segredo")]
    [InlineData(".segredo-sem-id")]
    public async Task Token_malformado_deve_recusar(string token)
    {
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => CriarUseCase().ExecuteAsync(token));
    }

    [Fact]
    public async Task Sessao_revogada_deve_recusar()
    {
        // É o que faz logout, kick, troca de papel e reset de senha realmente
        // encerrarem o acesso: sem esta checagem o refresh token ressuscitaria
        // a sessão.
        var sessao = SessaoValida();
        sessao.Revoke();
        ArranjarSessao(sessao, UsuarioAtivo());

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => CriarUseCase().ExecuteAsync($"{SessionId}.segredo-antigo"));
    }

    [Fact]
    public async Task Sessao_expirada_deve_recusar()
    {
        var sessao = new Session("192.168.1.10", DateTimeOffset.UtcNow.AddMinutes(-1), "hash-antigo", Guid.NewGuid())
        {
            Id = SessionId
        };
        ArranjarSessao(sessao, UsuarioAtivo());

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => CriarUseCase().ExecuteAsync($"{SessionId}.segredo-antigo"));
    }

    [Fact]
    public async Task Segredo_errado_deve_recusar()
    {
        var sessao = SessaoValida();
        ArranjarSessao(sessao, UsuarioAtivo());
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => CriarUseCase().ExecuteAsync($"{SessionId}.segredo-chutado"));
    }

    [Fact]
    public async Task Conta_desativada_no_meio_da_sessao_nao_deve_renovar()
    {
        // Sem esta checagem, quem fosse bloqueado continuaria entrando pelos 7
        // dias de validade do refresh token.
        var sessao = SessaoValida();
        var user = UsuarioAtivo();
        user.Deactivate();
        ArranjarSessao(sessao, user);

        await Assert.ThrowsAsync<AccountDisabledException>(
            () => CriarUseCase().ExecuteAsync($"{SessionId}.segredo-antigo"));
    }

    [Fact]
    public async Task Sessao_inexistente_deve_recusar()
    {
        _users.GetSessionByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Session?)null);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => CriarUseCase().ExecuteAsync($"{SessionId}.segredo-antigo"));
    }
}
