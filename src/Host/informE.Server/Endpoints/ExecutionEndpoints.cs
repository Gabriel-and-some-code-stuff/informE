using System.Security.Claims;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Contracts.Dtos.Api;
using informE.Domain;
using informE.Domain.Enums;
using informE.Server.Auth;

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
        .WithDescription(
            "Constante do domínio, não vem do banco. A UI escolhe uma ação daqui e manda o `kind` " +
            "em POST /tasks — NUNCA um script, que é metade prática do RF14.")
        .Produces<List<MachineActionDto>>()
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
                CreatedByUserId: usuario.Id(),
                TargetDeviceIds: corpo.DeviceIds,
                TargetGroupIds: corpo.GroupIds), ct);

            // O Code (EX-1000) é gerado pelo banco no INSERT — só existe depois do
            // save, então relemos a task para devolvê-lo à tela.
            var task = await repositorio.GetByIdAsync(resposta.TaskId, ct);

            return Results.Ok(new DispatchTaskResponseDto(
                resposta.TaskId, task?.Code ?? string.Empty, resposta.DispatchedCount));
        })
        .WithName("DispararExecucao")
        .WithSummary("Dispara uma ação do catálogo em N dispositivos e/ou grupos.")
        .WithDescription(
            "Os envios saem em PARALELO, um por máquina. Máquina que aparece num deviceId e também " +
            "num grupo recebe o comando uma vez só. Máquina offline no disparo não trava a execução: " +
            "aquele log fecha como Failed e a tarefa consegue terminar.")
        .Produces<DispatchTaskResponseDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/", async (
            IMachineTaskRepository repositorio,
            IUserRepository usuarios,
            CancellationToken ct,
            int limite = 50) =>
        {
            var tasks = await repositorio.ListRecentAsync(limite, ct);

            // Nome de quem disparou (item 8 das pendências do front).
            //
            // MachineTask.CreatedByUserId é coluna solta — não existe navegação
            // para User nem FK no banco. Criar o relacionamento agora exigiria
            // migração com FK sobre dados já gravados, risco desnecessário; um
            // lookup pelos ids DISTINTOS resolve em uma consulta a mais, não uma
            // por linha.
            var idsDosAutores = tasks.Select(t => t.CreatedByUserId).Distinct().ToList();
            var nomePorId = new Dictionary<Guid, string>();

            foreach (var id in idsDosAutores)
            {
                var autor = await usuarios.GetByIdAsync(id, ct);

                if (autor is not null)
                    nomePorId[id] = autor.Username;
            }

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
                    l.OutputLog,
                    nomePorId.GetValueOrDefault(t.CreatedByUserId))))
                .OrderByDescending(i => i.ExecutedAt)
                .ToList();

            return Results.Ok(itens);
        })
        .WithName("ListarExecucoes")
        .WithSummary("Execuções recentes, uma linha por máquina.")
        .WithDescription(
            "O grão é por MÁQUINA, não por tarefa: uma ação disparada em 20 devices vira 20 linhas. " +
            "`limite` conta TAREFAS, então o número de linhas devolvidas é maior que ele.")
        .Produces<List<ExecutionListItemDto>>();

        grupo.MapGet("/{id:guid}", async (
            Guid id,
            IMachineTaskRepository repositorio,
            CancellationToken ct) =>
        {
            var task = await repositorio.GetByIdAsync(id, ct);

            if (task is null)
                return Results.NotFound();

            var itens = task.ExecutionLogs
                .Select(l => new ExecutionListItemDto(
                    l.Id, task.Id, task.Code, l.Device?.Hostname ?? "—",
                    l.ActionType, l.Status.ToString(), l.ExecutedAt, l.DurationMs, l.OutputLog))
                .OrderBy(i => i.DeviceHostname)
                .ToList();

            return Results.Ok(new TaskDetailDto(task.Id, task.Code, task.Status.ToString(), itens));
        })
        .WithName("ObterExecucao")
        .WithSummary("Detalhe de uma execução: status da tarefa + uma linha por máquina.")
        .WithDescription(
            "É o endpoint para acompanhar um disparo em N máquinas — o status da tarefa fecha " +
            "quando nenhum log está mais pendente.")
        .Produces<TaskDetailDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelTaskUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("CancelarExecucao")
        .WithSummary("Cancela uma execução pendente, enfileirada ou em andamento.")
        .WithDescription(
            "Cancela do lado do Host. Uma máquina que JÁ recebeu o comando continua executando — " +
            "interromper de verdade exigiria um método novo no contrato IAgentClient, que não existe.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
