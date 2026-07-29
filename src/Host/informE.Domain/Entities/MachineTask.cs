using informE.Domain.Enums;
using TaskStatus = informE.Domain.Enums.TaskStatus;

namespace informE.Domain.Entities;

// O disparo: "rodar este script nestes N endpoints".
public class MachineTask
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceScript { get; set; } = string.Empty; // ponytail: inline por ora; extrair tabela SCRIPTS no sprint 3-4
    public DateTimeOffset ScheduledAt { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    public Guid CreatedByUserId { get; set; }
    public ICollection<Device> TargetDevices { get; set; } = [];
    public ICollection<TaskExecutionLog> ExecutionLogs { get; set; } = [];

    public MachineTask() { }

    // Construtor padrão

    public MachineTask(string name, string sourceScript, DateTimeOffset scheduledAt, TaskStatus status, Guid createdByUserId)
    {
        Name = name;
        SourceScript = sourceScript;
        ScheduledAt = scheduledAt; // Hora não automática pois o usuário pode programar
        Status = status;
        CreatedByUserId = createdByUserId;
    }
}
