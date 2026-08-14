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
        if (ValidateName(name))
            Name = name;

        if (ValidateSourceScript(sourceScript))
            SourceScript = sourceScript;

        ScheduledAt = scheduledAt; // Hora não automática pois o usuário pode programar

        if (ValidateStatus(status))
            Status = status;

        CreatedByUserId = createdByUserId;
    }

    // Métodos de validação
    private static bool ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da tarefa não pode ser vazio.");

        if (name.Length > 100)
            throw new ArgumentException("O nome da tarefa ultrapassou o limite de caracteres.");

        return true;
    }

    private static bool ValidateSourceScript(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            throw new ArgumentException("O script não pode ser vazio ou conter espaços.");

        return true;
    }

    private static bool ValidateStatus(TaskStatus status)
    {
        return Enum.IsDefined(typeof(TaskStatus), status);
    }

    // Transições de ciclo de vida — Application layer é responsável por chamar na ordem correta.
    // Status final (Succeeded/Failed) é decidido pelo Application com base nos ExecutionLogs.
    public void Queue()
    {
        if (Status != TaskStatus.Pending)
            throw new InvalidOperationException("Apenas tarefas pendentes podem ser enfileiradas.");

        Status = TaskStatus.Queued;
    }

    public void MarkRunning()
    {
        if (Status != TaskStatus.Queued)
            throw new InvalidOperationException("Apenas tarefas enfileiradas podem ser marcadas como em execução.");

        Status = TaskStatus.Running;
    }

    public void Finish(bool succeeded)
    {
        if (Status != TaskStatus.Running)
            throw new InvalidOperationException("Apenas tarefas em execução podem ser finalizadas.");

        Status = succeeded ? TaskStatus.Succeeded : TaskStatus.Failed;
    }

    public void Cancel()
    {
        if (Status is TaskStatus.Running or TaskStatus.Succeeded or TaskStatus.Failed)
            throw new InvalidOperationException("Não é possível cancelar uma tarefa em execução ou já finalizada.");

        Status = TaskStatus.Canceled;
    }
}
