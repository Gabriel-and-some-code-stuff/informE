using System.Security.Claims;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Contracts.Dtos.Api;
using informE.Domain;
using informE.Domain.Enums;

namespace informE.Server.Endpoints;

public static class ExecutionEndpoints
{
    public static IEndpointRouteBuilder MapExecutionEndpoints(this IEndpointRouteBuilder app)
    {
        // O catálogo de ações não depende de banco nem de usuário — é constante do
        // domínio. Mesmo assim exige login: revelar quais comandos o sistema sabe
        // rodar é informação útil demais para quem não entrou.
        app.MapGet("/actions", () =>
        {
            var acoes = MachineActionCatalog.All
                .Select(a => new MachineActionDto(a.Kind.ToString(), a.DisplayName, a.Description))
                .ToList();

            return Results.Ok(acoes);
        })
        .WithTags("Execuções")
        .WithName("ListarAcoes")
        .WithSummary("Catálogo de ações que podem ser executadas (alimenta o dropdown).")
        .RequireAuthorization();

        var grupo = app.MapGroup("/tasks").WithTags("Execuções").RequireAuthorization();

        grupo.MapPost("/", async (
            DispatchTaskRequestDto corpo,
            ClaimsPrincipal usuario,
            DispatchTaskUseCase useCase,
            IMachineTaskRepository repositorio,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<MachineActionKind>(corpo.Action, ignoreCase: true, out var acao))
                return Results.BadRequest(new { erro = $"Ação '{corpo.Action}' não existe no catálogo." });

            var definicao = MachineActionCatalog.Get(acao);

            var resposta = await useCase.ExecuteAsync(new DispatchTaskRequest(
                Name: definicao.DisplayName,
                Action: acao,
                // Sem agendamento = agora. O toggle "agendar execução" da tela
                // manda a data; desligado, manda null.
                ScheduledAt: corpo.ScheduledAt ?? DateTimeOffset.UtcNow,
                CreatedByUserId: usuario.ObterUserId(),
                TargetDeviceIds: corpo.DeviceIds,
                TargetGroupIds: corpo.GroupIds), ct);

            // O Code (EX-1000) é gerado pelo banco no INSERT — só existe depois do
            // save, então relemos a task para devolvê-lo à tela.
            var task = await repositorio.GetByIdAsync(resposta.TaskId, ct);

            return Results.Ok(new DispatchTaskResponseDto(
                resposta.TaskId, task?.Code ?? string.Empty, resposta.DispatchedCount));
        })
        .WithName("DispararExecucao")
        .WithSummary("Dispara uma ação do catálogo em N dispositivos e/ou grupos.");

        grupo.MapGet("/", async (
            IMachineTaskRepository repositorio,
            CancellationToken ct,
            int limite = 50) =>
        {
            var tasks = await repositorio.ListRecentAsync(limite, ct);

            // A tela lista por MÁQUINA, não por tarefa: uma tarefa disparada em 20
            // devices vira 20 linhas. Por isso o achatamento em cima dos logs.
            var itens = tasks
                .SelectMany(t => t.ExecutionLogs.Select(l => new ExecutionListItemDto(
                    l.Id,
                    t.Id,
                    t.Code,
                    l.Device?.Hostname ?? "—",
                    l.ActionType,
                    l.Status.ToString(),
                    l.ExecutedAt,
                    l.DurationMs,
                    l.OutputLog)))
                .OrderByDescending(i => i.ExecutedAt)
                .ToList();

            return Results.Ok(itens);
        })
        .WithName("ListarExecucoes")
        .WithSummary("Execuções recentes, uma linha por máquina.");

        grupo.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelTaskUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("CancelarExecucao")
        .WithSummary("Cancela uma execução pendente, enfileirada ou em andamento.");

        return app;
    }

    // O `sub` do JWT é o Id do usuário — colocado lá pelo JwtTokenService.
    private static Guid ObterUserId(this ClaimsPrincipal usuario)
    {
        var bruto = usuario.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? usuario.FindFirstValue("sub");

        return Guid.TryParse(bruto, out var id)
            ? id
            : throw new UnauthorizedAccessException("Token sem identificação de usuário.");
    }
}
