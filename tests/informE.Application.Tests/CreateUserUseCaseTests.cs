using informE.Application;
using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Domain.Entities;
using informE.Domain.Enums;
using NSubstitute;

namespace informE.Application.Tests;

public class CreateUserUseCaseTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public CreateUserUseCaseTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("hash-fake");
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
    }

    // Lista vazia = sem restricao de dominio. Os testes de papel nao devem
    // depender do dominio do e-mail; a regra tem teste proprio em
    // DominioDeEmailPolicyTests.
    private CreateUserUseCase CriarUseCase() => new(_users, _hasher, new DominioDeEmailPolicy([]), _uow);

    // 8 e o minimo que CreateUserUseCase exige. A constante deixa claro que os
    // testes de PAPEL nao estao testando senha -- so precisam de uma valida.
    private const string SenhaValida = "senha12345";

    private static CreateUserRequest Request(UserRole papel) =>
        new("prof", "prof@cps.sp.gov.br", SenhaValida, papel);

    // SuperAdmin cria Admin e Viewer. Admin cria SOMENTE Viewer.
    [Theory]
    [InlineData(UserRole.SuperAdmin, UserRole.SuperAdmin)]
    [InlineData(UserRole.SuperAdmin, UserRole.Admin)]
    [InlineData(UserRole.SuperAdmin, UserRole.Viewer)]
    [InlineData(UserRole.Admin, UserRole.Viewer)]
    public async Task Deve_permitir_as_combinacoes_da_regra(UserRole criador, UserRole alvo)
    {
        var id = await CriarUseCase().ExecuteAsync(Request(alvo), criador);

        Assert.NotEqual(Guid.Empty, id);
        await _users.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Admin, UserRole.Admin)]        // Admin não promove ao próprio nível
    [InlineData(UserRole.Admin, UserRole.SuperAdmin)]   // nem acima
    [InlineData(UserRole.Viewer, UserRole.Viewer)]      // Viewer não cria ninguém
    [InlineData(UserRole.Viewer, UserRole.Admin)]
    public async Task Deve_recusar_o_que_esta_fora_da_regra(UserRole criador, UserRole alvo)
    {
        await Assert.ThrowsAsync<ForbiddenRoleAssignmentException>(
            () => CriarUseCase().ExecuteAsync(Request(alvo), criador));

        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Email_duplicado_deve_lancar()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new User("outro", "prof@cps.sp.gov.br", "hash", UserRole.Viewer));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarUseCase().ExecuteAsync(Request(UserRole.Viewer), UserRole.Admin));
    }

    [Fact]
    public async Task Senha_deve_ser_hasheada_nunca_persistida_em_claro()
    {
        User? persistido = null;
        await _users.AddAsync(Arg.Do<User>(u => persistido = u), Arg.Any<CancellationToken>());

        await CriarUseCase().ExecuteAsync(Request(UserRole.Viewer), UserRole.Admin);

        Assert.NotNull(persistido);
        Assert.NotEqual(SenhaValida, persistido.PasswordHash);
        _hasher.Received().Hash(SenhaValida);
    }

    // ── Regras que fecharam dois 500 ─────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("1234567")]
    public async Task Senha_curta_deve_lancar(string senha)
    {
        var request = new CreateUserRequest("prof", "prof@cps.sp.gov.br", senha, UserRole.Viewer);

        await Assert.ThrowsAsync<ArgumentException>(
            () => CriarUseCase().ExecuteAsync(request, UserRole.Admin));

        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nome_duplicado_deve_lancar_em_vez_de_estourar_no_banco()
    {
        // `users.username` tem indice UNICO. Sem esta checagem o INSERT chegava
        // no Postgres, violava ix_users_username (23505) e a tela recebia um 500
        // "Erro interno" no lugar de "esse nome ja esta em uso".
        _users.GetByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new User("prof", "outro@cps.sp.gov.br", "hash", UserRole.Viewer));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarUseCase().ExecuteAsync(Request(UserRole.Viewer), UserRole.Admin));

        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Papel_e_verificado_ANTES_da_senha()
    {
        // Ordem importa: quem nao pode criar aquele papel nao deve descobrir
        // nada sobre as outras regras. Viewer tentando criar Admin recebe
        // Forbidden, nao "a senha e curta".
        var request = new CreateUserRequest("prof", "prof@cps.sp.gov.br", "123", UserRole.Admin);

        await Assert.ThrowsAsync<ForbiddenRoleAssignmentException>(
            () => CriarUseCase().ExecuteAsync(request, UserRole.Viewer));
    }
}
