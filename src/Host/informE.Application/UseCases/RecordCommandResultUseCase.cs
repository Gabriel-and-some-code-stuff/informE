using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Contracts.Dtos;
using TaskStatus = informE.Domain.Enums.TaskStatus;

namespace informE.Application.UseCases;

// RF09 (retorno de status/stdout/stderr) + a metade "Coletar Logs" de
// "Visualizar Logs" no diagrama de casos de uso — disparado pelo AgentHub
// quando o agente devolve o resultado de um comando. RN02 (agente descarta o
// log local após confirmação do Host) é satisfeito implicitamente: o método
// SignalR correspondente só retorna sem exceção depois que este use case
// persistir com sucesso, e essa resposta É a confirmação que o agente espera.
public class RecordCommandResultUseCase(
    IMachineTaskRepository machineTaskRepository,
    IUnitOfWork unitOfWork,
    IDashboardNotifier dashboardNotifier)
{
    public async Task<RecordCommandResultResponse> ExecuteAsync(CommandResultDto result, CancellationToken ct = default)
    {
        var logStatus = result.Succeeded ? TaskStatus.Succeeded : TaskStatus.Failed;

        await machineTaskRepository.UpdateLogStatusAsync(result.LogId, logStatus, result.Output, result.ExecutedAt, result.DurationMs, ct);

        var task = await machineTaskRepository.GetByIdAsync(result.TaskId, ct)
            ?? throw new InvalidOperationException($"MachineTask {result.TaskId} não encontrado.");

        var stillPending = task.ExecutionLogs.Any(l => l.Status is TaskStatus.Pending or TaskStatus.Running);
        if (stillPending)
            return new RecordCommandResultResponse(TaskCompleted: false, TaskSucceeded: null);

        var allSucceeded = task.ExecutionLogs.All(l => l.Status == TaskStatus.Succeeded);
        task.Finish(allSucceeded);
        await unitOfWork.SaveChangesAsync(ct);

        await dashboardNotifier.TaskProgressAsync(task.Id, task.Status, ct);

        return new RecordCommandResultResponse(TaskCompleted: true, TaskSucceeded: allSucceeded);
    }
}
