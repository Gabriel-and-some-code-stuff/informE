using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class NetworkGrowthRepository(AppDbContext db) : INetworkGrowthRepository
{
    // Idempotente por dia: se o job rodar duas vezes, sobrescreve a linha de hoje
    // em vez de duplicar (a tabela tem grão de 1 linha por dia do tenant).
    public async Task SnapshotTodayAsync(int totalDevices, int totalGroups, CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(DateTimeOffset.Now.Date);

        var existente = await db.NetworkGrowthSnapshots
            .FirstOrDefaultAsync(s => s.Date == hoje, ct);

        if (existente is null)
        {
            await db.NetworkGrowthSnapshots.AddAsync(new NetworkGrowthSnapshot(totalDevices, totalGroups), ct);
            return;
        }

        existente.TotalDevices = totalDevices;
        existente.TotalGroups = totalGroups;
    }

    public Task<List<NetworkGrowthSnapshot>> ListByRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        db.NetworkGrowthSnapshots
            .Where(s => s.Date >= from && s.Date <= to)
            .OrderBy(s => s.Date)
            .ToListAsync(ct);
}
