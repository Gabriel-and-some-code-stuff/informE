using informE.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace informE.Server.Auth;

// Traduz exceção de negócio em status HTTP, num lugar só.
//
// Sem isto, ou cada endpoint repete try/catch, ou toda falha de regra vira 500 —
// e em produção o cliente receberia stack trace. Aqui a API devolve
// ProblemDetails (RFC 7807) com a mensagem que a exceção já carrega, e o stack
// fica no log do servidor.
public class ExcecaoParaHttpHandler(ILogger<ExcecaoParaHttpHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception excecao, CancellationToken ct)
    {
        var (status, titulo) = Traduzir(excecao);

        // 5xx é bug nosso e vai como erro; 4xx é o usuário/cliente errando e vira
        // ruído no log se subir como erro.
        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(excecao, "Erro não tratado em {Rota}", context.Request.Path);
        else
            logger.LogInformation("{Status} em {Rota}: {Mensagem}", status, context.Request.Path, excecao.Message);

        context.Response.StatusCode = status;

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = titulo,
            // Mensagem de 500 é genérica de propósito: exceção inesperada pode
            // conter detalhe de infraestrutura (nome de tabela, connection string).
            Detail = status >= StatusCodes.Status500InternalServerError
                ? "Erro interno. Consulte os logs do servidor."
                : excecao.Message,
            Instance = context.Request.Path,
        }, ct);

        return true;
    }

    private static (int Status, string Titulo) Traduzir(Exception excecao) => excecao switch
    {
        InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Credenciais inválidas"),

        AccountDisabledException => (StatusCodes.Status403Forbidden, "Conta desativada"),
        ForbiddenRoleAssignmentException => (StatusCodes.Status403Forbidden, "Operação não permitida"),
        UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Operação não permitida"),

        EnrollmentTokenInvalidException => (StatusCodes.Status400BadRequest, "Token de registro inválido"),
        InvalidResetTokenException => (StatusCodes.Status400BadRequest, "Link de redefinição inválido"),
        ArgumentException => (StatusCodes.Status400BadRequest, "Requisição inválida"),

        // Limite de dispositivos e transição de estado inválida são CONFLITO:
        // a requisição está bem formada, mas o estado atual não permite.
        DeviceLimitReachedException => (StatusCodes.Status409Conflict, "Limite de dispositivos atingido"),
        InvalidOperationException => (StatusCodes.Status409Conflict, "Operação não permitida no estado atual"),

        _ => (StatusCodes.Status500InternalServerError, "Erro interno"),
    };
}
