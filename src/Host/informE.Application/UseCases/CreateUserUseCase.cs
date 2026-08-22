using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Domain.Entities;
using informE.Domain.Enums;

namespace informE.Application.UseCases;

// CRUD User — "+ Novo Usuário" na tela de Administração de Contas.
public class CreateUserUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
{
    // SuperAdmin cria Admin e Viewer. Admin cria SOMENTE Viewer.
    //
    // O documento de análise do Figma diz "o ADMIN pode criar ADMIN E VIEWER",
    // mas isso foi corrigido pelo time: Admin não promove ninguém ao próprio
    // nível. Bate com docs/politica-login-sessao.md §1, que já dizia que Admin
    // gerencia apenas Usuários Comuns.
    //
    // Em aberto: SuperAdmin pode criar outro SuperAdmin? Hoje NÃO — a regra
    // ditada foi "ADMIN E VIEWER". Se puder, é uma linha aqui.
    private static readonly Dictionary<UserRole, UserRole[]> PodeCriar = new()
    {
        [UserRole.SuperAdmin] = [UserRole.Admin, UserRole.Viewer],
        [UserRole.Admin] = [UserRole.Viewer],
        [UserRole.Viewer] = [],
    };

    public async Task<Guid> ExecuteAsync(CreateUserRequest request, UserRole criadoPor, CancellationToken ct = default)
    {
        if (!PodeCriar.TryGetValue(criadoPor, out var permitidos) || !permitidos.Contains(request.Role))
            throw new ForbiddenRoleAssignmentException(criadoPor, request.Role);

        var jaExiste = await userRepository.GetByEmailAsync(request.Email, ct);
        if (jaExiste is not null)
            throw new InvalidOperationException($"Já existe usuário com o e-mail {request.Email}.");

        // O construtor de User valida username/email/role e lança se inválido.
        var user = new User(
            request.Username,
            request.Email,
            passwordHasher.Hash(request.Password),
            request.Role)
        {
            // Id gerado aqui, não pelo gen_random_uuid() do banco: mesma escolha
            // do DispatchTaskUseCase e do EnrollDeviceUseCase. Depender do
            // round-trip do EF pra saber o Id deixaria o retorno indefinido antes
            // do SaveChanges.
            Id = Guid.NewGuid()
        };

        await userRepository.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return user.Id;
    }
}
