using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.UseCases;
using informE.Domain.Entities;
using informE.Domain.Enums;
using NSubstitute;

namespace informE.Application.Tests;

public class ChangeUserRoleUseCaseTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public ChangeUserRoleUseCaseTests()
    {
        // Contrato devolve List não-nulo; o mock precisa refletir isso.
        _users.GetActiveSessionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private ChangeUserRoleUseCase CriarUseCase() => new(_users, _uow);

    private User Alvo(UserRole papel = UserRole.Admin)
    {
        var user = new User("tecnico", "tecnico@etec.sp.gov.br", "hash", papel) { Id = Guid.NewGuid() };
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    // "SuperAdmin pode subir o cargo de um admin"
    [Fact]
    public async Task SuperAdmin_deve_poder_promover_admin_a_superadmin()
    {
        var alvo = Alvo(UserRole.Admin);

        await CriarUseCase().ExecuteAsync(alvo.Id, UserRole.SuperAdmin, Guid.NewGuid(), UserRole.SuperAdmin);

        Assert.Equal(UserRole.SuperAdmin, alvo.Role);
    }

    [Fact]
    public async Task SuperAdmin_deve_poder_rebaixar()
    {
        var alvo = Alvo(UserRole.Admin);

        await CriarUseCase().ExecuteAsync(alvo.Id, UserRole.Viewer, Guid.NewGuid(), UserRole.SuperAdmin);

        Assert.Equal(UserRole.Viewer, alvo.Role);
    }

    // Se Admin pudesse mudar papel, poderia se promover e a hierarquia sumiria.
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Viewer)]
    public async Task Quem_nao_e_superadmin_nao_deve_mexer_em_papel(UserRole solicitante)
    {
        var alvo = Alvo(UserRole.Viewer);

        await Assert.ThrowsAsync<ForbiddenRoleAssignmentException>(
            () => CriarUseCase().ExecuteAsync(alvo.Id, UserRole.Admin, Guid.NewGuid(), solicitante));

        Assert.Equal(UserRole.Viewer, alvo.Role);
    }

    [Fact]
    public async Task Ninguem_deve_alterar_o_proprio_papel()
    {
        // Trava contra auto-rebaixamento acidental: sem isto o único SuperAdmin
        // poderia se rebaixar e deixar a instância sem ninguém que promova.
        var eu = Alvo(UserRole.SuperAdmin);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarUseCase().ExecuteAsync(eu.Id, UserRole.Viewer, eu.Id, UserRole.SuperAdmin));

        Assert.Equal(UserRole.SuperAdmin, eu.Role);
    }

    [Fact]
    public async Task Mudanca_de_papel_deve_derrubar_as_sessoes()
    {
        // O papel vai como claim no access token. Sem revogar, um rebaixamento só
        // valeria quando o token de 15 min vencesse.
        var alvo = Alvo(UserRole.Admin);
        var sessao = new Session("192.168.0.10", DateTimeOffset.Now.AddDays(7), "h", alvo.Id);
        _users.GetActiveSessionsAsync(alvo.Id, Arg.Any<CancellationToken>()).Returns([sessao]);

        await CriarUseCase().ExecuteAsync(alvo.Id, UserRole.Viewer, Guid.NewGuid(), UserRole.SuperAdmin);

        Assert.False(sessao.IsActive);
    }

    [Fact]
    public async Task Mesmo_papel_deve_ser_idempotente()
    {
        var alvo = Alvo(UserRole.Admin);

        await CriarUseCase().ExecuteAsync(alvo.Id, UserRole.Admin, Guid.NewGuid(), UserRole.SuperAdmin);

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
