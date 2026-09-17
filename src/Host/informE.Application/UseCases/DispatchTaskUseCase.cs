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
    IDashboardNotifier dashboardNotifier,
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
                executedAt: DateTimeOffset.UtcNow, // placeholder — sobrescrito pelo ExecutedAt real quando o resultado chegar (ver RecordCommandResultUseCase)
                machineTaskId: task.Id,
                deviceId: deviceId))
            .ToList();

        await machineTaskRepository.AddWithLogsAsync(task, logs, ct);

        task.Queue();
        task.MarkRunning();

        await unitOfWork.SaveChangesAsync(ct);

        // ── Despacho ──────────────────────────────────────────────────────────
        // EM PARALELO. Antes era um foreach com await, o que serializava N envios
        // SignalR independentes — "executar em 20 máquinas ao mesmo tempo" virava
        // 20 envios em fila. Pior: uma exceção no device nº 3 abortava o loop, e
        // os 17 restantes nunca recebiam o comando.
        //
        // Só o ENVIO é paralelo. A persistência do resultado vem depois, em
        // sequência, porque o AppDbContext não é thread-safe — escrever a partir
        // de N tarefas concorrentes estouraria "a second operation was started on
        // this context instance".
        var entregas = await Task.WhenAll(logs.Select(log => TentarDespacharAsync(task, log, ct)));

        var falhas = entregas.Where(e => !e.Entregue).ToList();

        foreach (var falha in falhas)
            falha.Log.FalharNoDespacho(falha.Motivo!);

        // Máquina offline fecha o log como Failed AGORA. Sem isso,
        // RecordCommandResultUseCase nunca veria a tarefa completa —
        // `stillPending` seria eternamente true — e a execução ficaria Running
        // para sempre na tela. Com 7 das 105 máquinas do seed offline, toda
        // execução em grupo caía nisso.
        //
        // Mutação nas entidades JÁ rastreadas, não ExecuteUpdate: os logs foram
        // criados neste mesmo DbContext, então uma escrita por SQL direto ficaria
        // invisível para o change tracker e a checagem abaixo leria Pending.
        var tarefaFechou = false;

        if (falhas.Count > 0 && logs.TrueForAll(l => l.Status is not (TaskStatus.Pending or TaskStatus.Running)))
        {
            task.Finish(logs.TrueForAll(l => l.Status == TaskStatus.Succeeded));
            tarefaFechou = true;
        }

        if (falhas.Count > 0)
            await unitOfWork.SaveChangesAsync(ct);

        // Notificação depois do save: a tela não pode receber "concluída" antes
        // de o banco conseguir confirmá-la.
        foreach (var falha in falhas)
            await dashboardNotifier.ExecutionLogUpdatedAsync(
                falha.Log.Id, task.Id, TaskStatus.Failed, 0, falha.Motivo, ct);

        if (tarefaFechou)
            await dashboardNotifier.TaskProgressAsync(task.Id, task.Status, ct);

        return new DispatchTaskResponse(task.Id, logs.Count);
    }

    private record Entrega(TaskExecutionLog Log, bool Entregue, string? Motivo);

    // Só toca no hub e no log — nada de banco, porque roda em paralelo.
    private async Task<Entrega> TentarDespacharAsync(MachineTask task, TaskExecutionLog log, CancellationToken ct)
    {
        var command = new CommandDto(task.Id, log.Id, task.SourceScript, task.Kind.ToString());

        try
        {
            return await commandDispatcher.DispatchAsync(log.DeviceId, command, ct)
                ? new Entrega(log, true, null)
                : new Entrega(log, false, "Máquina offline no momento do disparo.");
        }
        catch (Exception ex)
        {
            // Falha de envio de UMA máquina não pode derrubar as outras nem
            // estourar 500 no técnico que disparou.
            //
            // Sem ILogger de propósito: o motivo vira o OutputLog daquele log, que
            // é persistido e aparece na tela de Execuções. Um log de servidor seria
            // a mesma informação num lugar onde ninguém do time olha, e obrigaria a
            // Application a depender de Microsoft.Extensions.Logging.
            return new Entrega(log, false, $"Falha ao enviar o comando: {ex.Message}");
        }
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
