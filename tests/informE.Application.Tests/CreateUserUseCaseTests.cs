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

    private CreateUserUseCase CriarUseCase() => new(_users, _hasher, _uow);

    private static CreateUserRequest Request(UserRole papel) =>
        new("prof", "prof@etec.sp.gov.br", "senha", papel);

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
            .Returns(new User("outro", "prof@etec.sp.gov.br", "hash", UserRole.Viewer));

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
        Assert.NotEqual("senha", persistido.PasswordHash);
        _hasher.Received().Hash("senha");
    }
}
