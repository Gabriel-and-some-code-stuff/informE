using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class AlertRepository(AppDbContext db) : IAlertRepository
{
    public async Task AddAsync(Alert alert, CancellationToken ct = default) =>
        await db.Alerts.AddAsync(alert, ct);

    // Alimenta o gráfico "Histórico de Alertas" (stacked bar por dia/tipo) e o
    // painel "Alertas Recentes". Alert.OccurredAt é DateTimeOffset e o filtro
    // vem em DateOnly, então o range é montado como [from 00:00, to+1 00:00).
    public Task<List<Alert>> ListByRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var inicio = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var fim = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        return db.Alerts
            .Where(a => a.OccurredAt >= inicio && a.OccurredAt < fim)
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync(ct);
    }
}
