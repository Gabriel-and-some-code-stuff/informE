using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using informE.Domain.Enums;

namespace informE.Application.UseCases;

// Comentário #242 do Figma: "desconectar sessão/dispositivo, em cada um deles".
// É o botão de revogar do painel de dispositivos ativos (Meu Perfil / Adm. de
// Contas) e também o que destrava o 4º dispositivo quando o Admin bate no limite
// de 3 — ver docs/politica-login-sessao.md §2.1.
public class RevokeSessionUseCase(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid sessionId, Guid solicitadoPorUserId, UserRole solicitadoPorRole, CancellationToken ct = default)
    {
        var session = await userRepository.GetSessionByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"Sessão {sessionId} não encontrada.");

        // Dono revoga a própria sessão. Admin/SuperAdmin revogam a de terceiro
        // (é o que permite desbloquear um usuário travado no limite de dispositivos).
        var ehDono = session.UserId == solicitadoPorUserId;
        var ehPrivilegiado = solicitadoPorRole is UserRole.Admin or UserRole.SuperAdmin;

        if (!ehDono && !ehPrivilegiado)
            throw new UnauthorizedAccessException("Você só pode encerrar as suas próprias sessões.");

        // Idempotente: revogar sessão já revogada não é erro — o botão pode ter
        // sido clicado duas vezes, ou a sessão pode ter caído nesse meio tempo.
        if (!session.IsActive)
            return;

        session.Revoke();
        await unitOfWork.SaveChangesAsync(ct);
    }
}
