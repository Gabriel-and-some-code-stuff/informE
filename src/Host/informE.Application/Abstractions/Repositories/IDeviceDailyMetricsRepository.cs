using informE.Domain.Entities;

namespace informE.Application.Abstractions.Repositories;

public interface IDeviceDailyMetricsRepository
{
    // Cria a linha do dia se não existir, ou atualiza uptime/picos (upsert incremental).
    Task UpsertAsync(Guid deviceId, DateOnly date, int uptimeSeconds,
        float peakCpuPercent, float peakRamPercent, float peakDiskPercent,
        int activeUsersCount, CancellationToken ct = default);

    Task<List<DeviceDailyMetrics>> ListByDeviceAsync(
        Guid deviceId, DateOnly from, DateOnly to, CancellationToken ct = default);

    // Purga registros mais antigos que a janela de retenção do tenant.
    Task PurgeOlderThanAsync(DateOnly cutoff, CancellationToken ct = default);
}
