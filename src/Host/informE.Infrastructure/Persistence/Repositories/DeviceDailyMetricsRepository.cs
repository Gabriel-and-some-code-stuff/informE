using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class DeviceDailyMetricsRepository(AppDbContext db) : IDeviceDailyMetricsRepository
{
    // Upsert por (device, dia). A agregação dos picos é do Domain
    // (DeviceDailyMetrics.ApplyReading), não daqui — o repositório só decide
    // entre criar a linha do dia ou aplicar a leitura na que já existe.
    public async Task UpsertAsync(Guid deviceId, DateOnly date, int uptimeSeconds,
        float peakCpuPercent, float peakRamPercent, float peakDiskPercent,
        int activeUsersCount, CancellationToken ct = default)
    {
        var existente = await db.DeviceDailyMetrics
            .FirstOrDefaultAsync(m => m.DeviceId == deviceId && m.Date == date, ct);

        if (existente is null)
        {
            var nova = new DeviceDailyMetrics(deviceId, uptimeSeconds,
                peakCpuPercent, peakRamPercent, peakDiskPercent, activeUsersCount)
            {
                // O construtor assume "hoje"; aqui a data vem explícita porque uma
                // telemetria atrasada pode chegar depois da meia-noite.
                Date = date
            };

            await db.DeviceDailyMetrics.AddAsync(nova, ct);
            return;
        }

        existente.ApplyReading(uptimeSeconds, peakCpuPercent, peakRamPercent, peakDiskPercent, activeUsersCount);
    }

    // Alimenta o filtro de 7/15 dias do dashboard.
    public Task<List<DeviceDailyMetrics>> ListByDeviceAsync(
        Guid deviceId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        db.DeviceDailyMetrics
            .Where(m => m.DeviceId == deviceId && m.Date >= from && m.Date <= to)
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

    public Task PurgeOlderThanAsync(DateOnly cutoff, CancellationToken ct = default) =>
        db.DeviceDailyMetrics
            .Where(m => m.Date < cutoff)
            .ExecuteDeleteAsync(ct);
}
