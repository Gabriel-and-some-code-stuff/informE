using informE.Domain.Enums;
using TaskStatus = informE.Domain.Enums.TaskStatus;

namespace informE.Domain.Entities;

// 1 registro por (task, device) — resultado da execução em cada máquina.
public class TaskExecutionLog
{
    public Guid Id { get; set; }
    public string ActionType { get; set; } = "";
    public TaskStatus Status { get; set; }
    public string? OutputLog { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }

    public Guid MachineTaskId { get; set; }
    public MachineTask MachineTask { get; set; } = null!;

    public Guid DeviceId { get; set; } // coluna ausente no schema original — adicionada no port
    public Device Device { get; set; } = null!;
}
