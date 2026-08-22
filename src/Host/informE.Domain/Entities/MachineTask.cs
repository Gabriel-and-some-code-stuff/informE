using informE.Domain.Enums;
using TaskStatus = informE.Domain.Enums.TaskStatus;

namespace informE.Domain.Entities;

// O disparo: "rodar esta ação nestes N endpoints".
public class MachineTask
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // A ação escolhida no dropdown. É a fonte da verdade: SourceScript e Kind
    // são derivados dela pelo catálogo, nunca vêm do cliente.
    public MachineActionKind Action { get; set; }

    // Script resolvido no momento do disparo. Persistido para auditoria — se o
    // catálogo mudar depois, o log continua mostrando o que de fato rodou.
    public string SourceScript { get; set; } = string.Empty;
    public ScriptKind Kind { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    public Guid CreatedByUserId { get; set; }
    public ICollection<Device> TargetDevices { get; set; } = [];
    public ICollection<TaskExecutionLog> ExecutionLogs { get; set; } = [];

    public MachineTask() { }

    // Construtor padrão — recebe a AÇÃO, não o script. Isso torna impossível
    // criar uma task com script arbitrário vindo da UI (RF14).
    public MachineTask(string name, MachineActionKind action, DateTimeOffset scheduledAt, TaskStatus status, Guid createdByUserId)
    {
        if (ValidateName(name))
            Name = name;

        var definition = MachineActionCatalog.Get(action);
        Action = action;
        SourceScript = definition.Script;
        Kind = definition.ScriptKind;

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

    // Running É cancelável: a tela de Execuções mostra botão de parar na linha
    // "Executando" (EX-2846). Só tarefa já finalizada não pode ser cancelada.
    public void Cancel()
    {
        if (Status is TaskStatus.Succeeded or TaskStatus.Failed or TaskStatus.Canceled)
            throw new InvalidOperationException("Não é possível cancelar uma tarefa já finalizada.");

        Status = TaskStatus.Canceled;
    }
}
