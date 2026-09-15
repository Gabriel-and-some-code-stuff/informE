using System.Security.Claims;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Contracts.Dtos.Api;
using informE.Infrastructure.Email;
using informE.Server.Auth;
using Microsoft.Extensions.Options;

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

            return Results.Ok(Converter(resposta));
        })
        .WithName("Login")
        .WithSummary("Autentica e devolve access token + refresh token.")
        .WithDescription(
            "O access token vale 15 minutos; o refresh token, 7 dias. E-mail inexistente e senha " +
            "errada devolvem o MESMO 401 — diferenciar permitiria enumerar contas.")
        .Produces<LoginResponseDto>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .AllowAnonymous();

        grupo.MapPost("/refresh", async (
            RefreshRequestDto corpo,
            RefreshTokenUseCase useCase,
            CancellationToken ct) =>
        {
            var resposta = await useCase.ExecuteAsync(corpo.RefreshToken, ct);
            return Results.Ok(Converter(resposta));
        })
        .WithName("RenovarToken")
        .WithSummary("Troca um refresh token válido por um par de tokens novo.")
        .WithDescription(
            "O refresh token é ROTACIONADO: o token enviado deixa de valer nesta mesma chamada. " +
            "Guarde o novo. Conta desativada no meio da sessão não renova (403).")
        .Produces<LoginResponseDto>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .AllowAnonymous();

        grupo.MapPost("/logout", async (
            ClaimsPrincipal usuario,
            RevokeSessionUseCase useCase,
            CancellationToken ct) =>
        {
            // Sem o claim `sid` não há o que revogar. Acontece com token emitido
            // por uma versão anterior do servidor — tratado como logout bem
            // sucedido: o cliente vai descartar o token de qualquer forma.
            var sessaoAtual = usuario.SessaoAtual();
            if (sessaoAtual is null)
                return Results.NoContent();

            await useCase.ExecuteAsync(sessaoAtual.Value, usuario.Id(), usuario.Papel(), ct);
            return Results.NoContent();
        })
        .WithName("Logout")
        .WithSummary("Encerra a sessão do dispositivo atual.")
        .WithDescription(
            "Revoga o refresh token desta sessão. O access token é stateless e sobrevive até " +
            "vencer (no máximo 15 min) — o cliente deve descartá-lo.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();

        grupo.MapPost("/forgot-password", async (
            ForgotPasswordRequestDto corpo,
            RequestPasswordResetUseCase useCase,
            IOptions<SmtpOptions> smtp,
            IHostEnvironment ambiente,
            CancellationToken ct) =>
        {
            var link = await useCase.ExecuteAsync(corpo.Email, smtp.Value.ResetPasswordUrl, ct);

            // O link só sai na resposta em DESENVOLVIMENTO e SEM SMTP.
            //
            // Sem isto o fluxo era indemonstravel: nao ha servidor de e-mail em
            // maquina de desenvolvimento, entao o token nascia e ninguem o
            // recebia. Em producao (ou com SMTP configurado) o campo vem null e
            // o link viaja apenas pelo e-mail, como deve.
            var expor = ambiente.IsDevelopment() && string.IsNullOrWhiteSpace(smtp.Value.Host);

            return Results.Ok(new ForgotPasswordResponseDto(expor ? link : null));
        })
        .WithName("PedirRedefinicaoDeSenha")
        .WithSummary("Envia link de redefinição para o e-mail institucional.")
        .WithDescription(
            "Devolve 200 SEMPRE, exista a conta ou não — resposta diferente permitiria descobrir " +
            "quais e-mails têm conta. Conta desativada também não recebe link. " +
            "`linkDeDesenvolvimento` só vem preenchido em Development e sem SMTP configurado; " +
            "em produção é sempre null e o link viaja apenas por e-mail.")
        .Produces<ForgotPasswordResponseDto>()
        .AllowAnonymous();

        grupo.MapPost("/reset-password", async (
            ResetPasswordRequestDto corpo,
            ResetPasswordUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(corpo.Token, corpo.NovaSenha, ct);
            return Results.NoContent();
        })
        .WithName("RedefinirSenha")
        .WithSummary("Define a nova senha a partir do token recebido por e-mail.")
        .WithDescription(
            "O token tem formato {id}.{segredo}, vale 1 hora e é de uso único. Redefinir revoga " +
            "todas as sessões do usuário.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .AllowAnonymous();

        return app;
    }

    private static LoginResponseDto Converter(LoginResponse r) => new(
        r.AccessToken, r.RefreshToken, r.RefreshTokenExpiresAt, r.UserId, r.Username, r.Role.ToString());

    // Session.DeviceLabel tem limite de coluna e o User-Agent completo é enorme.
    private static string? Encurtar(string? userAgent) =>
        string.IsNullOrWhiteSpace(userAgent)
            ? null
            : userAgent.Length <= 100 ? userAgent : userAgent[..100];
}
