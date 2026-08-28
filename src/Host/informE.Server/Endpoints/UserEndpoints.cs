using System.Security.Claims;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Contracts.Dtos.Api;
using informE.Domain.Enums;

namespace informE.Server.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/users").WithTags("Usuários").RequireAuthorization();

        // Só Admin/SuperAdmin criam usuário. CreateUserUseCase decide QUAL papel
        // cada um pode atribuir (Admin só cria Viewer, ver o dicionário PodeCriar
        // lá dentro) — aqui só barra quem não é nem um nem outro.
        grupo.MapPost("/", async (
            CreateUserRequestDto corpo,
            ClaimsPrincipal usuario,
            CreateUserUseCase useCase,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<UserRole>(corpo.Role, ignoreCase: true, out var papel))
                return Results.BadRequest($"Papel inválido: {corpo.Role}. Use Viewer, Admin ou SuperAdmin.");

            var papelDoCriador = usuario.FindFirstValue(ClaimTypes.Role);
            if (!Enum.TryParse<UserRole>(papelDoCriador, ignoreCase: true, out var criadoPor))
                throw new UnauthorizedAccessException("Token sem papel identificado.");

            var userId = await useCase.ExecuteAsync(
                new CreateUserRequest(corpo.Username, corpo.Email, corpo.Password, papel), criadoPor, ct);

            return Results.Ok(new CreateUserResponseDto(userId));
        })
        .WithName("CriarUsuario")
        .WithSummary("Cria um usuário (Admin/SuperAdmin) — botão \"+ Novo Usuário\" da tela de Administração de Contas.")
        .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        return app;
    }
}
