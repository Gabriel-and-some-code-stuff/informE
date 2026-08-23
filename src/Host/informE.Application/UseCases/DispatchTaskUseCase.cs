using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Contracts.Dtos;
using informE.Domain;
using informE.Domain.Entities;
using TaskStatus = informE.Domain.Enums.TaskStatus;

namespace informE.Application.UseCases;

// RF09 (execução via CommandId) + RF10 (fila de comandos por Agent, persistida
// antes de despachar). "Gerenciar Tarefas" -> "Executar Tarefas" no diagrama de
// casos de uso: Técnico/Admin dispara, N TaskExecutionLog nascem Pending, task
// vai Pending -> Queued -> Running nesta mesma chamada (não há fila assíncrona
// real nesta versão — dispatch acontece no mesmo request).
//
// RF11 (controle de concorrência) é responsabilidade do Agent, não do Host —
// não implementado aqui.
public class DispatchTaskUseCase(
    IMachineTaskRepository machineTaskRepository,
    IDeviceRepository deviceRepository,
    ICommandDispatcher commandDispatcher,
    IUnitOfWork unitOfWork)
{
    public async Task<DispatchTaskResponse> ExecuteAsync(DispatchTaskRequest request, CancellationToken ct = default)
    {
        var alvos = await ResolverAlvosAsync(request, ct);

        if (alvos.Count == 0)
            throw new ArgumentException("A tarefa precisa de ao menos um dispositivo alvo.");

        // O construtor resolve o script pelo catálogo — nada de script do cliente.
        var task = new MachineTask(request.Name, request.Action, request.ScheduledAt, TaskStatus.Pending, request.CreatedByUserId);

        // Id gerado no cliente (não pelo gen_random_uuid() do Postgres) porque
        // os TaskExecutionLog abaixo precisam do MachineTaskId ANTES do primeiro
        // SaveChanges — o default do banco só resolveria depois do insert.
        task.Id = Guid.NewGuid();

        // Nome de exibição da ação ("Atualização WinGet") — é o que a coluna
        // "Ação Executada" da tela de Execuções mostra.
        var actionName = MachineActionCatalog.Get(request.Action).DisplayName;

        var logs = alvos
            .Select(deviceId => new TaskExecutionLog(
                actionType: actionName,
                status: TaskStatus.Pending,
                outputLog: null,
                executedAt: DateTimeOffset.Now, // placeholder — sobrescrito pelo ExecutedAt real quando o resultado chegar (ver RecordCommandResultUseCase)
                machineTaskId: task.Id,
                deviceId: deviceId))
            .ToList();

        await machineTaskRepository.AddWithLogsAsync(task, logs, ct);

        task.Queue();
        task.MarkRunning();

        await unitOfWork.SaveChangesAsync(ct);

        // ICommandDispatcher ainda não tem implementação real (depende do
        // AgentHub, Semana 3) — quando existir, revisar isolamento de falha
        // por device aqui (hoje uma exceção de um device aborta o restante do
        // loop; não dá pra decidir a semântica certa sem saber como o hub
        // real se comporta com device offline).
        foreach (var log in logs)
        {
            var command = new CommandDto(task.Id, log.Id, task.SourceScript, task.Kind.ToString());
            await commandDispatcher.DispatchAsync(log.DeviceId, command, ct);
        }

        return new DispatchTaskResponse(task.Id, logs.Count);
    }

    // Une dispositivos escolhidos um a um + os dispositivos dos grupos escolhidos.
    // O HashSet garante que máquina presente nas duas listas não receba o comando
    // duas vezes (nem gere dois TaskExecutionLog).
    private async Task<List<Guid>> ResolverAlvosAsync(DispatchTaskRequest request, CancellationToken ct)
    {
        var alvos = new HashSet<Guid>(request.TargetDeviceIds);

        foreach (var groupId in request.TargetGroupIds ?? [])
        {
            var doGrupo = await deviceRepository.ListByGroupAsync(groupId, ct);
            foreach (var device in doGrupo)
                alvos.Add(device.Id);
        }

        return [.. alvos];
    }
}
