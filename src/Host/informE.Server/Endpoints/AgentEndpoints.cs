using System.Security.Claims;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Contracts.Dtos.Api;

namespace informE.Server.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Registro da máquina (RF01/RF12) ───────────────────────────────────
        // ANÔNIMO de propósito: o agente ainda não tem credencial nenhuma — é
        // justamente isso que ele vem buscar. Quem autoriza é o EnrollmentToken,
        // que só um admin logado consegue emitir (endpoint abaixo), é de uso único
        // e expira em 2 horas.
        app.MapPost("/agent/enroll", async (
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
        .WithTags("Agente")
        .WithName("RegistrarAgente")
        .WithSummary("Registra a máquina e devolve a chave permanente do agente.")
        .AllowAnonymous();

        // ── Emissão do token de registro ──────────────────────────────────────
        app.MapPost("/admin/enrollment-tokens", async (
            ClaimsPrincipal usuario,
            CreateEnrollmentTokenUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = usuario.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? usuario.FindFirstValue("sub");

            if (!Guid.TryParse(userId, out var criadoPor))
                throw new UnauthorizedAccessException("Token sem identificação de usuário.");

            var token = await useCase.ExecuteAsync(criadoPor, ct);

            return Results.Ok(new EnrollmentTokenResponseDto(token, DateTimeOffset.UtcNow.AddHours(2)));
        })
        .WithTags("Agente")
        .WithName("GerarTokenDeRegistro")
        .WithSummary("Gera token de uso único para registrar uma máquina nova.")
        // Só Admin e SuperAdmin: quem tem este token consegue colocar uma máquina
        // dentro do parque monitorado.
        .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        return app;
    }
}
