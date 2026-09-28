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

        // Empurra ANTES de checar se a tarefa acabou: é este evento que faz a
        // linha daquela máquina mudar na hora na tela de Execuções. Sem ele, o
        // operador que disparou em 20 máquinas via a tela inteira congelada até
        // a última responder.
        await dashboardNotifier.ExecutionLogUpdatedAsync(
            result.LogId, result.TaskId, logStatus, result.DurationMs, result.Output, ct);

        // Caminho quente: este método roda uma vez POR MÁQUINA. A pergunta
        // "ainda falta alguém?" é um EXISTS indexado, não um carregamento da
        // tarefa inteira com logs e devices — que, numa execução em 21 máquinas,
        // significaria 21 carregamentos completos para ler alguns status.
        if (await machineTaskRepository.HasPendingLogsAsync(result.TaskId, ct))
            return new RecordCommandResultResponse(TaskCompleted: false, TaskSucceeded: null);

        // Só a ÚLTIMA máquina chega aqui: uma carga completa por tarefa, não por
        // máquina.
        var task = await machineTaskRepository.GetByIdAsync(result.TaskId, ct)
            ?? throw new InvalidOperationException($"MachineTask {result.TaskId} não encontrado.");

        var allSucceeded = task.ExecutionLogs.All(l => l.Status == TaskStatus.Succeeded);
        task.Finish(allSucceeded);
        await unitOfWork.SaveChangesAsync(ct);

        await dashboardNotifier.TaskProgressAsync(task.Id, task.Status, ct);

        return new RecordCommandResultResponse(TaskCompleted: true, TaskSucceeded: allSucceeded);
    }
}
