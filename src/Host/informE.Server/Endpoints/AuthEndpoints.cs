using informE.Application.Models;
using informE.Application.UseCases;
using informE.Contracts.Dtos.Api;

namespace informE.Server.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/auth").WithTags("Autenticação");

        grupo.MapPost("/login", async (
            LoginRequestDto corpo,
            HttpContext http,
            LoginUseCase useCase,
            CancellationToken ct) =>
        {
            // IP e User-Agent vêm da REQUISIÇÃO, nunca do corpo: se o cliente
            // pudesse informar o próprio IP, o log de auditoria e o painel de
            // dispositivos viravam ficção.
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            var deviceLabel = http.Request.Headers.UserAgent.ToString();

            var resposta = await useCase.ExecuteAsync(
                new LoginRequest(corpo.Email, corpo.Password, ip, Encurtar(deviceLabel)), ct);

            return Results.Ok(new LoginResponseDto(
                resposta.AccessToken,
                resposta.RefreshToken,
                resposta.RefreshTokenExpiresAt,
                resposta.UserId,
                resposta.Username,
                resposta.Role.ToString()));
        })
        .WithName("Login")
        .WithSummary("Autentica e devolve access token + refresh token.")
        .AllowAnonymous();

        return app;
    }

    // Session.DeviceLabel tem limite de coluna e o User-Agent completo é enorme.
    private static string? Encurtar(string? userAgent) =>
        string.IsNullOrWhiteSpace(userAgent)
            ? null
            : userAgent.Length <= 100 ? userAgent : userAgent[..100];
}
