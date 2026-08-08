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
        if (!string.IsNullOrEmpty(name) && name.Length < 100)
            Name = name;

        SourceScript = sourceScript;
        ScheduledAt = scheduledAt; // Hora não automática pois o usuário pode programar

        if (ValidateStatus(status))
            Status = status;

        CreatedByUserId = createdByUserId;
    }

    // Métodos de validação
    public bool ValidateStatus(TaskStatus status)
    {
        return Enum.IsDefined(typeof(TaskStatus), status);
    }

    // Precisamos criar o método para validar o caminho do script

    // Nenhum método de domínio porque depois da execução não terá como alterar nada
}
