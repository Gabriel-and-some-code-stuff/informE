using informE.Server.Auth;
using System.Security.Claims;
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
            IGroupRepository grupos,
            ClaimsPrincipal usuario,
            CancellationToken ct,
            Guid? grupoId = null,
            string? status = null,
            string? busca = null) =>
        {
            // ESCOPO DO VIEWER -- decidido no servidor.
            //
            // Antes, /devices devolvia as 105 maquinas para qualquer papel: um
            // Viewer via o parque inteiro. A politica
            // (docs/politica-login-sessao.md §1) diz "acesso apenas aos proprios
            // dados/maquinas".
            //
            // Se o Viewer pedir um grupoId que nao e dele, o pedido nao vira
            // erro: vira lista vazia. Recusar com 403 confirmaria que o grupo
            // existe, e isso permite descobrir o parque por tentativa.
            if (usuario.Papel() == UserRole.Viewer)
            {
                var meus = await grupos.ListByOwnerAsync(usuario.Id(), ct);
                var idsPermitidos = meus.Select(g => g.Id).ToHashSet();

                if (grupoId is not null && !idsPermitidos.Contains(grupoId.Value))
                    return Results.Ok(new DeviceListResponseDto(new DeviceSummaryDto(0, 0, 0, 0), []));

                // Sem grupoId, o Viewer recebe o(s) laboratorio(s) dele. Sem
                // nenhum laboratorio atribuido, recebe vazio -- nunca o parque.
                if (grupoId is null)
                {
                    if (idsPermitidos.Count == 0)
                        return Results.Ok(new DeviceListResponseDto(new DeviceSummaryDto(0, 0, 0, 0), []));

                    grupoId = idsPermitidos.First();
                }
            }

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
                d.LastSeenAt,
                d.CpuPercent,
                d.RamPercent,
                d.DiskPercent)).ToList();

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
        .WithSummary("Lista equipamentos com filtro de grupo, conexão e busca por nome/IP/SO.")
        .WithDescription(
            "O resumo (total/online/offline/comProblema) é calculado sobre os itens FILTRADOS, " +
            "não sobre o banco inteiro — os big numbers têm que acompanhar o filtro aplicado. " +
            "Status inválido na query vira \"sem filtro\" em vez de erro.")
        .Produces<DeviceListResponseDto>();

        grupo.MapGet("/{id:guid}", async (
            Guid id,
            IDeviceRepository repositorio,
            IGroupRepository grupos,
            ClaimsPrincipal usuario,
            CancellationToken ct) =>
        {
            var d = await repositorio.GetByIdAsync(id, ct);

            if (d is null)
                return Results.NotFound();

            // Maquina de outro laboratorio responde 404, nao 403: 403 confirmaria
            // que o id existe, e com isso da para mapear o parque por tentativa.
            if (usuario.Papel() == UserRole.Viewer)
            {
                var meus = await grupos.ListByOwnerAsync(usuario.Id(), ct);

                if (d.GroupId is null || !meus.Any(g => g.Id == d.GroupId.Value))
                    return Results.NotFound();
            }

            var linha = new DeviceListItemDto(
                d.Id, d.Hostname, d.Group?.Name, d.LastIp, d.Os,
                d.Status.ToString(), d.Health.ToString(), d.Role.ToString(),
                d.UptimeSeconds, d.LastSeenAt,
                d.CpuPercent, d.RamPercent, d.DiskPercent);

            // Hardware vem null quando a maquina nunca teve coleta — a tela
            // mostra "Nao disponivel". Hoje so as maquinas do seed tem esses
            // dados; o agente nao coleta hardware (ver docs/pendencias-front-auditoria.md).
            var hardware = d.DeviceInfo is null
                ? null
                : new HardwareDto(
                    d.DeviceInfo.Cpu,
                    d.DeviceInfo.Gpu,
                    d.DeviceInfo.RamGb,
                    d.DeviceInfo.RamType.ToString(),
                    d.DeviceInfo.StorageGb,
                    d.DeviceInfo.StorageType.ToString(),
                    d.DeviceInfo.MotherBoard,
                    d.DeviceInfo.Bios,
                    d.DeviceInfo.CollectedAt);

            return Results.Ok(new DeviceDetailDto(linha, hardware));
        })
        .WithName("ObterDispositivo")
        .WithSummary("Detalhe de um equipamento, com hardware quando houver coleta.")
        .WithDescription(
            "O bloco `hardware` e null quando a maquina nunca teve coleta de inventario. " +
            "O agente atual NAO coleta hardware (so CPU/RAM/disco/uptime), portanto " +
            "apenas as maquinas do seed trazem esse bloco preenchido. A tela deve " +
            "exibir \"Nao disponivel\", nunca um valor inventado.")
        .Produces<DeviceDetailDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
