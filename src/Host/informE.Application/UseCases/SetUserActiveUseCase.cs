using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;

namespace informE.Application.UseCases;

// Coluna "Status" (Ativo/Inativo) + botão de bloquear na tela de Administração
// de Contas.
//
// Implementa docs/politica-login-sessao.md §3.5: desativar encerra TODAS as
// sessões ativas imediatamente, sem esperar heartbeat ou próximo login. É por
// isso que a revogação vive aqui e não em User.Deactivate() — o Domain não
// alcança o repositório.
public class SetUserActiveUseCase(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid userId, bool ativo, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException($"Usuário {userId} não encontrado.");

        if (ativo)
        {
            user.Activate();
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        user.Deactivate();

        var sessoes = await userRepository.GetActiveSessionsAsync(userId, ct);
        foreach (var sessao in sessoes)
            await userRepository.RevokeSessionAsync(sessao.Id, ct);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
