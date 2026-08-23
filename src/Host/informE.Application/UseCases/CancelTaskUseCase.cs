using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;

namespace informE.Application.UseCases;

// Botão de parar da tela de Execuções (linha "Executando", EX-2846).
//
// ⚠️ Cancela só do lado do HOST: marca a MachineTask como Canceled e para de
// aceitar o resultado como válido. O agente que já recebeu o comando continua
// executando — IAgentClient não tem método de cancelamento (só RunCommand e
// RotateKey). Cancelamento real no agente exige um método novo no contrato do hub.
public class CancelTaskUseCase(
    IMachineTaskRepository machineTaskRepository,
    IUnitOfWork unitOfWork,
    IDashboardNotifier dashboardNotifier)
{
    public async Task ExecuteAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await machineTaskRepository.GetByIdAsync(taskId, ct)
            ?? throw new InvalidOperationException($"MachineTask {taskId} não encontrada.");

        // Cancel() recusa tarefa já finalizada — a exceção sobe pra API virar 409.
        task.Cancel();

        await unitOfWork.SaveChangesAsync(ct);
        await dashboardNotifier.TaskProgressAsync(task.Id, task.Status, ct);
    }
}
