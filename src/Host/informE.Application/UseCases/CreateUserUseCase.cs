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
    DominioDeEmailPolicy dominioDeEmail,
    IUnitOfWork unitOfWork)
{
    // SuperAdmin cria qualquer papel, inclusive outro SuperAdmin.
    // Admin cria SOMENTE Viewer — não promove ninguém ao próprio nível.
    //
    // O documento de análise do Figma diz "o ADMIN pode criar ADMIN E VIEWER",
    // mas o time corrigiu. Bate com docs/politica-login-sessao.md §1.
    private static readonly Dictionary<UserRole, UserRole[]> PodeCriar = new()
    {
        [UserRole.SuperAdmin] = [UserRole.SuperAdmin, UserRole.Admin, UserRole.Viewer],
        [UserRole.Admin] = [UserRole.Viewer],
        [UserRole.Viewer] = [],
    };

    public async Task<Guid> ExecuteAsync(CreateUserRequest request, UserRole criadoPor, CancellationToken ct = default)
    {
        if (!PodeCriar.TryGetValue(criadoPor, out var permitidos) || !permitidos.Contains(request.Role))
            throw new ForbiddenRoleAssignmentException(criadoPor, request.Role);

        // Só e-mail institucional. Antes da checagem de duplicidade porque é mais
        // barata e a mensagem é mais útil: "não é institucional" explica o erro
        // melhor do que um 409 de e-mail que o usuário nem deveria poder usar.
        dominioDeEmail.Validar(request.Email);

        PoliticaDeSenha.Validar(request.Password);

        var emailOcupado = await userRepository.GetByEmailAsync(request.Email, ct);
        if (emailOcupado is not null)
            throw new InvalidOperationException($"Já existe usuário com o e-mail {request.Email}.");

        // `users.username` tambem e UNICO. Sem esta checagem o INSERT violava
        // ix_users_username, o Npgsql lancava 23505 e a tela recebia 500
        // "Erro interno" -- sem dizer que o problema era o nome repetido.
        var nomeOcupado = await userRepository.GetByUsernameAsync(request.Username, ct);
        if (nomeOcupado is not null)
            throw new InvalidOperationException($"Já existe usuário com o nome {request.Username}.");

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
