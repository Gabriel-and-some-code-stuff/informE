using informE.Domain.Enums;

namespace informE.Domain.Entities;

// "Endpoint" no domínio do produto — a máquina monitorada.
public class Device
{
    public Guid Id { get; set; }
    public string Hostname { get; set; } = "";
    public string LastIp { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public string Os { get; set; } = "";
    public string OsUser { get; set; } = "";
    public DateTimeOffset RegisteredAt { get; set; }
    public EndpointStatus Status { get; set; } = EndpointStatus.Unknown;
    public DateTimeOffset? LastSeenAt { get; set; }

    // Auth do agente: chave rotativa guardada com DPAPI no agente, hash aqui.
    public string AgentKeyHash { get; set; } = "";
    public DateTimeOffset KeyRotatedAt { get; set; }

    public Guid? GroupId { get; set; }
    public Group? Group { get; set; }
    public DeviceInfo? Info { get; set; }

    public ICollection<TaskExecutionLog> ExecutionLogs { get; set; } = [];
    public ICollection<MachineTask> Tasks { get; set; } = [];
    public ICollection<Software> InstalledSoftwares { get; set; } = [];
    public ICollection<DeviceDailyMetrics> DailyMetrics { get; set; } = [];
    public ICollection<Alert> Alerts { get; set; } = [];
}
