using informE.Domain.Entities;

namespace informE.Application.Abstractions.Repositories;

public interface IAlertRepository
{
    Task AddAsync(Alert alert, CancellationToken ct = default);

    // Contagem por dia/tipo — alimenta o gráfico "Histórico de Alertas" (stacked bar).
    Task<List<Alert>> ListByRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
