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

    public DeviceDailyMetrics () { }

    // Construtor para registro padrão
    public DeviceDailyMetrics(Guid deviceId, int uptimeSeconds, float peakCpuPercent, float peakRamPercent, float peakDiskPercent, int activeUsersCount)
    {
        DeviceId = deviceId;
        UptimeSeconds = uptimeSeconds;
        PeakCpuPercent = peakCpuPercent;
        PeakRamPercent = peakRamPercent;
        PeakDiskPercent = peakDiskPercent;
        ActiveUsersCount = activeUsersCount;
        Date = DateOnly.FromDateTime(DateTime.Today);
    }
}
