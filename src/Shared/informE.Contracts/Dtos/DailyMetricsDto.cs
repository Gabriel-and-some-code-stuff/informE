namespace informE.Contracts.Dtos;

// Agente calcula localmente (uptime acumulado do dia + picos observados) e
// manda incrementalmente pelo AgentHub — upsert por (DeviceId, Date) no Server.
public record DailyMetricsDto(
    Guid DeviceId,
    DateOnly Date,
    int UptimeSeconds,
    float PeakCpuPercent,
    float PeakRamPercent,
    float PeakDiskPercent,
    int ActiveUsersCount
);
