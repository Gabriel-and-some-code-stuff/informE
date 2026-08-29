using System.Security.Claims;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Contracts.Dtos.Api;
using informE.Infrastructure.Persistence.Seeding;
using informE.Server.Auth;

namespace informE.Server.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var agente = app.MapGroup("/agent").WithTags("Agente");
        var admin = app.MapGroup("/admin").WithTags("Agente").RequireAuthorization();

        // ── Registro da máquina (RF01/RF12) ───────────────────────────────────
        // ANÔNIMO de propósito: o agente ainda não tem credencial nenhuma — é
        // justamente isso que ele vem buscar. Quem autoriza é o EnrollmentToken,
        // que só um admin logado consegue emitir (endpoint abaixo), é de uso único
        // e expira em 2 horas.
        agente.MapPost("/enroll", async (
            EnrollRequestDto corpo,
            EnrollDeviceUseCase useCase,
            CancellationToken ct) =>
        {
            var resposta = await useCase.ExecuteAsync(new EnrollDeviceRequest(
                corpo.EnrollmentToken,
                corpo.Hostname,
                corpo.IpAddress,
                corpo.MacAddress,
                corpo.Os,
                corpo.OsUser,
                corpo.GroupId), ct);

            return Results.Ok(new EnrollResponseDto(resposta.DeviceId, resposta.AgentKey));
        })
        .WithName("RegistrarAgente")
        .WithSummary("Registra a máquina e devolve a chave permanente do agente.")
        .WithDescription(
            "A agentKey volta em texto claro UMA vez — o agente a guarda com DPAPI e ela nunca " +
            "mais é exibida. Máquina já registrada (mesmo MAC) não duplica: a chave é rotacionada " +
            "e o deviceId existente é devolvido.")
        .Produces<EnrollResponseDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .AllowAnonymous();

        // ── Emissão do token de registro ──────────────────────────────────────
        admin.MapPost("/enrollment-tokens", async (
            ClaimsPrincipal usuario,
            CreateEnrollmentTokenUseCase useCase,
            CancellationToken ct) =>
        {
            var (token, expiraEm) = await useCase.ExecuteAsync(usuario.Id(), ct);

            return Results.Ok(new EnrollmentTokenResponseDto(token, expiraEm));
        })
        .WithName("GerarTokenDeRegistro")
        .WithSummary("Gera token de uso único para registrar uma máquina nova.")
        .WithDescription("Vale 2 horas e serve para um registro só.")
        .Produces<EnrollmentTokenResponseDto>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        // Só Admin e SuperAdmin: quem tem este token consegue colocar uma máquina
        // dentro do parque monitorado.
        .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        return app;
    }

    // Rota separada porque só existe em Development — mapeá-la junto das outras
    // exigiria um `if` no meio do grupo, e a condição ficaria longe da rota.
    public static IEndpointRouteBuilder MapSeedEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/seed", async (
            DatabaseBootstrapper bootstrapper,
            CancellationToken ct) =>
        {
            var semeou = await bootstrapper.SeedDevelopmentDataAsync(ct);

            return Results.Ok(new
            {
                semeou,
                mensagem = semeou
                    ? "Massa de desenvolvimento aplicada."
                    : "Banco já tinha contas e parque — nada a fazer."
            });
        })
        .WithTags("Agente")
        .WithName("SemearBanco")
        .WithSummary("Repõe a massa de desenvolvimento sem derrubar o container.")
        .WithDescription(
            "Idempotente e em dois conjuntos independentes: contas e parque. Só existe em " +
            "Development — em produção esta rota não é mapeada.")
        .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));

        return app;
    }
}
