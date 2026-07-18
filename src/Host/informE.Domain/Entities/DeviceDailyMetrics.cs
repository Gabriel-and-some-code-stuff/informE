namespace informE.Domain.Entities;

// Um registro por (device, dia) — uptime + picos de recurso, reportados
// incrementalmente pelo próprio agente ao longo do dia (upsert).
public class DeviceDailyMetrics
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public DateOnly Date { get; set; }
    public int UptimeSeconds { get; set; }
    public float PeakCpuPercent { get; set; }
    public float PeakRamPercent { get; set; }
    public float PeakDiskPercent { get; set; }
    public int ActiveUsersCount { get; set; }
}
