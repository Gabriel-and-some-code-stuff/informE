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

    public Guid MachineTaskId { get; set; }
    public MachineTask MachineTask { get; set; } = null!;

    public Guid DeviceId { get; set; } // coluna ausente no schema original — adicionada no port
    public Device Device { get; set; } = null!;

    public TaskExecutionLog() { }

    // Construtor padrão 

    public TaskExecutionLog(string actionType, TaskStatus status, string? outputLog, DateTimeOffset executedAt, Guid machineTaskId, Guid deviceId)
    {
        ActionType = actionType;
        Status = status;
        OutputLog = outputLog;
        ExecutedAt = executedAt; // Não deixei automático pra que seja registrado a exata hora em que a execução for feita, não a criação do registro no bd 
        MachineTaskId = machineTaskId;
        DeviceId = deviceId;
    }
}
