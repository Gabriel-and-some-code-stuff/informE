using informE.Domain.Enums;
using TaskStatus = informE.Domain.Enums.TaskStatus;

namespace informE.Domain.Entities;

// 1 registro por (task, device) — resultado da execução em cada máquina.
public class TaskExecutionLog
{
    public Guid Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public string? OutputLog { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }

    // Coluna "Duração" da tela de Execuções. Medido pelo AGENTE (Stopwatch
    // local), não calculado no servidor: o Host não sabe quando o agente
    // realmente começou, só quando despachou. Null enquanto Pending/Queued —
    // a tela mostra "—" nessas linhas.
    public int? DurationMs { get; set; }

    public Guid MachineTaskId { get; set; }
    public MachineTask MachineTask { get; set; } = null!;

    public Guid DeviceId { get; set; } // coluna ausente no schema original — adicionada no port
    public Device Device { get; set; } = null!;

    public TaskExecutionLog() { }

    // Construtor padrão
    public TaskExecutionLog(string actionType, TaskStatus status, string? outputLog, DateTimeOffset executedAt, Guid machineTaskId, Guid deviceId)
    {
        ActionType = actionType;

        if (ValidateTaskStatus(status))
            Status = status;
        
        OutputLog = outputLog;
        ExecutedAt = executedAt; // Não deixei automático pra que seja registrado a exata hora em que a execução for feita, não a criação do registro no bd
        MachineTaskId = machineTaskId;
        DeviceId = deviceId;
    }

    private static bool ValidateTaskStatus(TaskStatus taskStatus)
    {
        return Enum.IsDefined(typeof(TaskStatus), taskStatus);
    }

    // O comando nem chegou a sair: máquina offline no disparo, ou falha de envio.
    //
    // Fechar como Failed em vez de deixar Pending é o que permite a tarefa
    // terminar. MachineTask só fecha quando nenhum log está pendente — um log
    // eternamente Pending deixava a execução inteira travada em Running.
    // DurationMs = 0 porque nada rodou; null significaria "ainda não sei".
    public void FalharNoDespacho(string motivo)
    {
        Status = TaskStatus.Failed;
        OutputLog = motivo;
        ExecutedAt = DateTimeOffset.UtcNow;
        DurationMs = 0;
    }
}
