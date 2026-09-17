using informE.Domain.Entities;

namespace informE.Application.Interfaces.Repositories;

public interface IAlertRepository
{
    Task AddAsync(Alert alert, CancellationToken ct = default);

    // Contagem por dia/tipo — alimenta o gráfico "Histórico de Alertas" (stacked bar),
    // o painel "Alertas Recentes" e o resumo de alertas da tela de Grupos.
    //
    // grupoId opcional: a tela de Grupos precisa do recorte por laboratório, e
    // filtrar depois de trazer tudo obrigaria a carregar o parque inteiro só
    // para contar os alertas de um lab.
    Task<List<Alert>> ListByRangeAsync(
        DateOnly from, DateOnly to, Guid? grupoId = null, CancellationToken ct = default);
}
