using informE.Application.Interfaces.Repositories;
using informE.Contracts.Dtos.Api;
using informE.Domain;
using informE.Domain.Enums;

namespace informE.Server.Endpoints;

// Alertas — item 5 do documento de pendências do frontend.
//
// Um endpoint só, em vez das seis rotas que o documento pediu (recentes, por
// período, por grupo, por categoria, contagem ativa, histórico do gráfico).
// Todas as seis são recortes do MESMO conjunto: alertas de um intervalo,
// opcionalmente de um grupo. O Dashboard precisa de quase todos ao mesmo tempo —
// seis rotas seriam seis viagens para montar uma tela.
//
// O que o documento pediu e NÃO existe: severidade. O domínio tem AlertType
// (11 valores técnicos) e AlertCategory (6 faixas de apresentação), nenhum
// conceito de gravidade. Registrado em docs/pendencias-front-auditoria.md.
public static class AlertEndpoints
{
    // Teto do intervalo. Sem limite, `?dias=100000` varreria a tabela inteira
    // numa chamada anônima de tela.
    private const int MaximoDeDias = 90;

    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/alerts").WithTags("Alertas").RequireAuthorization();

        grupo.MapGet("/", async (
            IAlertRepository repositorio,
            CancellationToken ct,
            int dias = 7,
            Guid? grupoId = null,
            string? categoria = null,
            int recentes = 20) =>
        {
            var janela = Math.Clamp(dias, 1, MaximoDeDias);
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            var inicio = hoje.AddDays(-(janela - 1));

            var alertas = await repositorio.ListByRangeAsync(inicio, hoje, grupoId, ct);

            // A categoria é derivada de AlertType em tempo de leitura, então o
            // filtro tem que acontecer aqui e não na query — não há coluna.
            var filtroCategoria = Enum.TryParse<AlertCategory>(categoria, ignoreCase: true, out var c)
                ? c
                : (AlertCategory?)null;

            var itens = alertas
                .Select(a => new
                {
                    Alerta = a,
                    Categoria = AlertCategoryMap.Of(a.Type)
                })
                .Where(x => filtroCategoria is null || x.Categoria == filtroCategoria)
                .ToList();

            // As 6 faixas sempre presentes, inclusive com zero: a tela desenha um
            // gráfico de barras empilhadas de largura fixa e não deveria ter que
            // completar faixa faltante.
            var todasAsFaixas = Enum.GetNames<AlertCategory>();

            var porCategoria = todasAsFaixas.ToDictionary(
                faixa => faixa,
                faixa => itens.Count(x => x.Categoria.ToString() == faixa));

            var historico = Enumerable.Range(0, janela)
                .Select(offset => inicio.AddDays(offset))
                .Select(dia =>
                {
                    var doDia = itens
                        .Where(x => DateOnly.FromDateTime(x.Alerta.OccurredAt.UtcDateTime) == dia)
                        .ToList();

                    return new AlertHistoryDayDto(
                        dia,
                        doDia.Count,
                        todasAsFaixas.ToDictionary(
                            faixa => faixa,
                            faixa => doDia.Count(x => x.Categoria.ToString() == faixa)));
                })
                .ToList();

            var lista = itens
                .Take(Math.Clamp(recentes, 1, 200))
                .Select(x => new AlertListItemDto(
                    x.Alerta.Id,
                    x.Alerta.DeviceId,
                    x.Alerta.Device.Hostname,
                    x.Alerta.Device.Group?.Name,
                    x.Alerta.Type.ToString(),
                    x.Categoria.ToString(),
                    x.Alerta.Message,
                    x.Alerta.OccurredAt))
                .ToList();

            return Results.Ok(new AlertsResponseDto(itens.Count, porCategoria, historico, lista));
        })
        .WithName("ListarAlertas")
        .WithSummary("Alertas do período, com histórico diário por categoria e as ocorrências recentes.")
        .WithDescription(
            "Uma chamada devolve os quatro recortes que o Dashboard precisa: total, contagem " +
            "por categoria, histórico dia a dia (para o gráfico de barras empilhadas) e a lista " +
            "de recentes. `dias` é limitado a 90. `categoria` aceita as 6 faixas " +
            "(Hardware, Armazenamento, Rede, Windows, Agente, Offline) e valor inválido vira " +
            "\"sem filtro\", igual ao filtro de status de /devices. " +
            "NÃO existe filtro por severidade: o domínio não tem esse conceito.")
        .Produces<AlertsResponseDto>();

        return app;
    }
}
