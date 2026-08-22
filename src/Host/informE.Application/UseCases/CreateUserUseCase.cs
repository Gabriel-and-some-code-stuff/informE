using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Domain.Entities;

namespace informE.Application.UseCases;

// CRUD User — "+ Novo Usuário" na tela de Administração de Contas.
//
// Quem pode criar quem (SuperAdmin cria qualquer perfil, Admin só cria Viewer) é
// autorização, não regra deste use case: fica no endpoint via claim de role —
// ver docs/politica-login-sessao.md §1.
public class CreateUserUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
{
    public async Task<Guid> ExecuteAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var jaExiste = await userRepository.GetByEmailAsync(request.Email, ct);
        if (jaExiste is not null)
            throw new InvalidOperationException($"Já existe usuário com o e-mail {request.Email}.");

        // O construtor de User valida username/email/role e lança se inválido.
        var user = new User(
            request.Username,
            request.Email,
            passwordHasher.Hash(request.Password),
            request.Role);

        await userRepository.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return user.Id;
    }
}
