using informE.Application.Interfaces.Repositories;
using informE.Contracts.Dtos.Api;
using informE.Domain.Enums;

namespace informE.Server.Endpoints;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/devices").WithTags("Equipamentos").RequireAuthorization();

        grupo.MapGet("/", async (
            IDeviceRepository repositorio,
            CancellationToken ct,
            Guid? grupoId = null,
            string? status = null,
            string? busca = null) =>
        {
            // Status inválido na query vira "sem filtro" em vez de 500. É filtro de
            // tela: o usuário digitou errado, não é falha do servidor.
            EndpointStatus? filtroStatus = Enum.TryParse<EndpointStatus>(status, ignoreCase: true, out var s)
                ? s
                : null;

            var devices = await repositorio.ListAsync(grupoId, filtroStatus, busca, ct);

            var itens = devices.Select(d => new DeviceListItemDto(
                d.Id,
                d.Hostname,
                d.Group?.Name,
                d.LastIp,
                d.Os,
                d.Status.ToString(),
                d.Health.ToString(),
                d.Role.ToString(),
                d.UptimeSeconds,
                d.LastSeenAt)).ToList();

            // O resumo é dos itens FILTRADOS, não do total do banco — os big
            // numbers têm que acompanhar o filtro que o usuário aplicou.
            var resumo = new DeviceSummaryDto(
                Total: itens.Count,
                Online: itens.Count(i => i.Status == nameof(EndpointStatus.Online)),
                Offline: itens.Count(i => i.Status == nameof(EndpointStatus.Offline)),
                ComProblema: itens.Count(i => i.Health is nameof(HealthStatus.Aviso)
                                                    or nameof(HealthStatus.Critico)
                                                    or nameof(HealthStatus.Erro)));

            return Results.Ok(new DeviceListResponseDto(resumo, itens));
        })
        .WithName("ListarDispositivos")
        .WithSummary("Lista equipamentos com filtro de grupo, conexão e busca por nome/IP/SO.");

        grupo.MapGet("/{id:guid}", async (
            Guid id,
            IDeviceRepository repositorio,
            CancellationToken ct) =>
        {
            var d = await repositorio.GetByIdAsync(id, ct);

            return d is null
                ? Results.NotFound()
                : Results.Ok(new DeviceListItemDto(
                    d.Id, d.Hostname, d.Group?.Name, d.LastIp, d.Os,
                    d.Status.ToString(), d.Health.ToString(), d.Role.ToString(),
                    d.UptimeSeconds, d.LastSeenAt));
        })
        .WithName("ObterDispositivo");

        return app;
    }
}
