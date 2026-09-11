using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.UseCases;
using informE.Domain.Entities;
using informE.Domain.Enums;
using NSubstitute;

namespace informE.Application.Tests;

public class PasswordResetTests
{
    private const string UrlBase = "https://informe.etec.local/redefinir-senha";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordResetTokenRepository _tokens = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public PasswordResetTests()
    {
        // Fake determinístico que NÃO carrega o texto original dentro do hash —
        // senão o teste de "não persistir em claro" passaria/falharia por causa do
        // dublê, não do código.
        _hasher.Hash(Arg.Any<string>()).Returns(call => FakeHash(call.Arg<string>()));
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(1) == FakeHash(call.ArgAt<string>(0)));

        // Contrato devolve List não-nulo; o mock precisa refletir isso.
        _users.GetActiveSessionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private static string FakeHash(string valor) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(valor));

    private RequestPasswordResetUseCase Pedir() => new(_users, _tokens, _hasher, _email, _uow);
    private ResetPasswordUseCase Redefinir() => new(_users, _tokens, _hasher, _uow);

    private static User Usuario() => new("prof", "prof@etec.sp.gov.br", "hash-antigo", UserRole.Viewer)
    {
        Id = Guid.NewGuid()
    };

    // ---------- pedir o link ----------

    [Fact]
    public async Task Email_inexistente_nao_deve_vazar_que_a_conta_nao_existe()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await Pedir().ExecuteAsync("naoexiste@etec.sp.gov.br", UrlBase);

        // Sem exceção e sem e-mail: de fora, indistinguível do caso de sucesso.
        await _email.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Conta_desativada_nao_deve_receber_link()
    {
        // Reset de senha não pode ser caminho de volta pra conta bloqueada.
        var user = Usuario();
        user.Deactivate();
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        await Pedir().ExecuteAsync(user.Email, UrlBase);

        await _email.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deve_mandar_o_link_para_o_email_institucional_do_cadastro()
    {
        var user = Usuario();
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        string? destinatario = null;
        string? corpo = null;
        await _email.SendAsync(
            Arg.Do<string>(t => destinatario = t), Arg.Any<string>(),
            Arg.Do<string>(b => corpo = b), Arg.Any<CancellationToken>());

        await Pedir().ExecuteAsync(user.Email, UrlBase);

        Assert.Equal(user.Email, destinatario);
        Assert.Contains(UrlBase, corpo);
    }

    [Fact]
    public async Task Segredo_do_link_nunca_deve_ser_persistido_em_claro()
    {
        var user = Usuario();
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        PasswordResetToken? persistido = null;
        string? corpo = null;
        await _tokens.AddAsync(Arg.Do<PasswordResetToken>(t => persistido = t), Arg.Any<CancellationToken>());
        await _email.SendAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Do<string>(b => corpo = b), Arg.Any<CancellationToken>());

        await Pedir().ExecuteAsync(user.Email, UrlBase);

        var segredo = ExtrairToken(corpo!).Split('.', 2)[1];
        Assert.NotNull(persistido);
        Assert.DoesNotContain(segredo, persistido.TokenHash);
        Assert.Equal(FakeHash(segredo), persistido.TokenHash);
    }

    [Fact]
    public async Task Pedir_link_novo_deve_invalidar_o_anterior()
    {
        var user = Usuario();
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        await Pedir().ExecuteAsync(user.Email, UrlBase);

        await _tokens.Received(1).InvalidateActiveForUserAsync(user.Id, Arg.Any<CancellationToken>());
    }

    // ---------- usar o link ----------

    [Fact]
    public async Task Token_valido_deve_trocar_a_senha_e_gastar_o_token()
    {
        var user = Usuario();
        var (token, textoDoToken) = TokenValidoPara(user);

        await Redefinir().ExecuteAsync(textoDoToken, "senha-nova");

        Assert.Equal(FakeHash("senha-nova"), user.PasswordHash);
        Assert.True(token.IsUsed);
    }

    [Fact]
    public async Task Troca_de_senha_deve_derrubar_as_sessoes_ativas()
    {
        var user = Usuario();
        var (_, textoDoToken) = TokenValidoPara(user);
        var sessao = new Session("192.168.0.10", DateTimeOffset.Now.AddDays(7), "h", user.Id);
        _users.GetActiveSessionsAsync(user.Id, Arg.Any<CancellationToken>()).Returns([sessao]);

        await Redefinir().ExecuteAsync(textoDoToken, "senha-nova");

        Assert.False(sessao.IsActive);
    }

    [Fact]
    public async Task Token_ja_usado_nao_deve_funcionar_de_novo()
    {
        var user = Usuario();
        var (token, textoDoToken) = TokenValidoPara(user);
        token.Redeem();

        await Assert.ThrowsAsync<InvalidResetTokenException>(
            () => Redefinir().ExecuteAsync(textoDoToken, "senha-nova"));
    }

    [Fact]
    public async Task Token_expirado_nao_deve_funcionar()
    {
        var user = Usuario();
        var (token, textoDoToken) = TokenValidoPara(user);
        token.ExpiresAt = DateTimeOffset.Now.AddMinutes(-1);

        await Assert.ThrowsAsync<InvalidResetTokenException>(
            () => Redefinir().ExecuteAsync(textoDoToken, "senha-nova"));
    }

    [Fact]
    public async Task Segredo_errado_com_id_valido_nao_deve_funcionar()
    {
        // Cenário do atacante que descobriu o Id (ele não é secreto) e tenta
        // adivinhar o segredo.
        var user = Usuario();
        var (token, _) = TokenValidoPara(user);

        await Assert.ThrowsAsync<InvalidResetTokenException>(
            () => Redefinir().ExecuteAsync($"{token.Id}.segredo-chutado", "senha-nova"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-ponto")]
    [InlineData("nao-e-guid.segredo")]
    [InlineData("11111111-1111-1111-1111-111111111111.")]
    public async Task Token_malformado_deve_dar_a_mesma_excecao(string tokenRuim)
    {
        await Assert.ThrowsAsync<InvalidResetTokenException>(
            () => Redefinir().ExecuteAsync(tokenRuim, "senha-nova"));
    }

    [Fact]
    public async Task Senha_nova_vazia_deve_ser_recusada()
    {
        var user = Usuario();
        var (_, textoDoToken) = TokenValidoPara(user);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Redefinir().ExecuteAsync(textoDoToken, "   "));
    }

    // Monta um token válido já "no banco" e devolve o texto que iria no link.
    private (PasswordResetToken Token, string TextoDoToken) TokenValidoPara(User user)
    {
        const string segredo = "segredo-de-teste";
        var token = new PasswordResetToken(user.Id, FakeHash(segredo)) { Id = Guid.NewGuid() };

        _tokens.GetByIdAsync(token.Id, Arg.Any<CancellationToken>()).Returns(token);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        return (token, $"{token.Id}.{segredo}");
    }

    private static string ExtrairToken(string corpoDoEmail)
    {
        var marcador = "?token=";
        var inicio = corpoDoEmail.IndexOf(marcador, StringComparison.Ordinal) + marcador.Length;
        var fim = corpoDoEmail.IndexOfAny([' ', '\n', '\r'], inicio);

        return fim < 0 ? corpoDoEmail[inicio..] : corpoDoEmail[inicio..fim];
    }
}
