using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Domain.Enums;

namespace informE.Application.UseCases;

// "SuperAdmin pode subir o cargo de um admin" — promover/rebaixar usuário na tela
// de Administração de Contas.
//
// Só SuperAdmin mexe em papel. Admin cria Viewer (CreateUserUseCase), mas não
// promove ninguém: se Admin pudesse mudar papel, poderia se promover a SuperAdmin
// e a hierarquia deixaria de existir.
public class ChangeUserRoleUseCase(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid userId, UserRole novoPapel, Guid solicitadoPorUserId, UserRole solicitadoPorRole, CancellationToken ct = default)
    {
        if (solicitadoPorRole != UserRole.SuperAdmin)
            throw new ForbiddenRoleAssignmentException(solicitadoPorRole, novoPapel);

        if (!Enum.IsDefined(novoPapel))
            throw new ArgumentException($"Papel {novoPapel} não existe.");

        // Trava de segurança: nem o SuperAdmin muda o próprio papel. Um
        // auto-rebaixamento acidental poderia deixar a instância sem nenhum
        // SuperAdmin, sem caminho de volta pela UI.
        if (userId == solicitadoPorUserId)
            throw new InvalidOperationException("Você não pode alterar o seu próprio papel.");

        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException($"Usuário {userId} não encontrado.");

        if (user.Role == novoPapel)
            return; // idempotente

        user.ChangeRole(novoPapel);

        // O papel viaja como claim no access token, que é stateless. Revogar as
        // sessões força novo login e novo token com o papel novo — senão um
        // rebaixamento só valeria quando o token de 15 min vencesse.
        var sessoes = await userRepository.GetActiveSessionsAsync(userId, ct);
        foreach (var sessao in sessoes)
            sessao.Revoke();

        await unitOfWork.SaveChangesAsync(ct);
    }
}
