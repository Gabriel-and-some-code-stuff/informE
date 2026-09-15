using System.Security.Claims;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Application.UseCases;
using informE.Contracts.Dtos.Api;
using informE.Domain.Entities;
using informE.Domain.Enums;
using informE.Server.Auth;

namespace informE.Server.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/users").WithTags("Usuários").RequireAuthorization();

        // ── Leitura ───────────────────────────────────────────────────────────

        // Antes de /{id:guid} de propósito: rota literal tem que ser avaliada
        // antes da paramétrica, senão "me" tentaria virar Guid.
        grupo.MapGet("/me", async (
            ClaimsPrincipal usuario,
            IUserRepository repositorio,
            CancellationToken ct) =>
        {
            var user = await repositorio.GetByIdAsync(usuario.Id(), ct);

            // Token válido de usuário que não existe mais: 401, não 404. O
            // problema é a credencial, não o recurso.
            return user is null
                ? Results.Unauthorized()
                : Results.Ok(Converter(user));
        })
        .WithName("MeuPerfil")
        .WithSummary("Dados da conta autenticada — tela Meu Perfil.")
        .Produces<UserListItemDto>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/me/sessions", async (
            ClaimsPrincipal usuario,
            IUserRepository repositorio,
            CancellationToken ct) =>
        {
            var sessaoAtual = usuario.SessaoAtual();
            var sessoes = await repositorio.GetActiveSessionsAsync(usuario.Id(), ct);

            var itens = sessoes
                .Where(s => !s.IsExpired())
                .OrderByDescending(s => s.LastSeenAt)
                .Select(s => new SessionListItemDto(
                    s.Id, s.DeviceLabel, s.IpAddress, s.LoginAt, s.LastSeenAt, s.ExpiresAt,
                    EhSessaoAtual: s.Id == sessaoAtual))
                .ToList();

            return Results.Ok(itens);
        })
        .WithName("MinhasSessoes")
        .WithSummary("Dispositivos com sessão ativa nesta conta.")
        .WithDescription(
            "Sessão vencida por tempo ainda aparece como IsActive no banco até a varredura passar, " +
            "então é filtrada aqui — senão a tela mostraria dispositivo que já não entra.")
        .Produces<List<SessionListItemDto>>();

        grupo.MapDelete("/me/sessions/{sessionId:guid}", async (
            Guid sessionId,
            ClaimsPrincipal usuario,
            RevokeSessionUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(sessionId, usuario.Id(), usuario.Papel(), ct);
            return Results.NoContent();
        })
        .WithName("EncerrarSessao")
        .WithSummary("Desconecta um dispositivo específico.")
        .WithDescription(
            "O dono encerra as próprias sessões; Admin/SuperAdmin encerram as de terceiros — é o " +
            "que destrava quem ficou preso no limite de 3 dispositivos. Idempotente.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapGet("/", async (
            IUserRepository repositorio,
            CancellationToken ct,
            string? papel = null,
            bool? ativo = null,
            string? busca = null) =>
        {
            // Papel inválido na query vira "sem filtro" em vez de 500 — mesmo
            // critério do filtro de status em GET /devices.
            UserRole? filtroPapel = Enum.TryParse<UserRole>(papel, ignoreCase: true, out var p) ? p : null;

            var users = await repositorio.ListAsync(filtroPapel, ativo, busca, ct);

            return Results.Ok(users.Select(Converter).ToList());
        })
        .WithName("ListarUsuarios")
        .WithSummary("Lista contas com filtro de papel, status e busca por nome/e-mail/código.")
        .Produces<List<UserListItemDto>>()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        grupo.MapGet("/{id:guid}", async (
            Guid id,
            IUserRepository repositorio,
            CancellationToken ct) =>
        {
            var user = await repositorio.GetByIdAsync(id, ct);

            return user is null ? Results.NotFound() : Results.Ok(Converter(user));
        })
        .WithName("ObterUsuario")
        .WithSummary("Detalhe de uma conta.")
        .Produces<UserListItemDto>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        // ── Escrita ───────────────────────────────────────────────────────────

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
                return Results.BadRequest(new { erro = $"Papel inválido: {corpo.Role}. Use Viewer, Admin ou SuperAdmin." });

            var userId = await useCase.ExecuteAsync(
                new CreateUserRequest(corpo.Username, corpo.Email, corpo.Password, papel), usuario.Papel(), ct);

            return Results.Ok(new CreateUserResponseDto(userId));
        })
        .WithName("CriarUsuario")
        .WithSummary("Cria um usuário — botão + Novo Usuário da tela de Administração de Contas.")
        .WithDescription(
            "SuperAdmin cria qualquer papel; Admin cria somente Viewer. O e-mail precisa ser de um " +
            "domínio institucional (ver Auth:DominiosPermitidos no appsettings).")
        .Produces<CreateUserResponseDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        grupo.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateUserRequestDto corpo,
            ClaimsPrincipal usuario,
            UpdateUserProfileUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(id, corpo.Username, corpo.Email, usuario.Id(), usuario.Papel(), ct);
            return Results.NoContent();
        })
        .WithName("AtualizarUsuario")
        .WithSummary("Atualiza nome de usuário e/ou e-mail.")
        .WithDescription(
            "O dono edita o próprio perfil; Admin/SuperAdmin editam o de terceiros. Campo nulo ou " +
            "vazio é ignorado. NÃO troca papel nem status — são endpoints separados de propósito.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPatch("/{id:guid}/role", async (
            Guid id,
            ChangeRoleRequestDto corpo,
            ClaimsPrincipal usuario,
            ChangeUserRoleUseCase useCase,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<UserRole>(corpo.Role, ignoreCase: true, out var papel))
                return Results.BadRequest(new { erro = $"Papel inválido: {corpo.Role}. Use Viewer, Admin ou SuperAdmin." });

            await useCase.ExecuteAsync(id, papel, usuario.Id(), usuario.Papel(), ct);
            return Results.NoContent();
        })
        .WithName("AlterarPapel")
        .WithSummary("Promove ou rebaixa um usuário.")
        .WithDescription(
            "Só SuperAdmin. Nem o SuperAdmin muda o próprio papel — sem essa trava, o único " +
            "SuperAdmin poderia se rebaixar e deixar a instância sem ninguém capaz de promover. " +
            "Alterar o papel REVOGA as sessões do alvo, porque o papel viaja no access token.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));

        grupo.MapPatch("/{id:guid}/active", async (
            Guid id,
            SetActiveRequestDto corpo,
            SetUserActiveUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(id, corpo.Ativo, ct);
            return Results.NoContent();
        })
        .WithName("AtivarOuDesativarUsuario")
        .WithSummary("Liga/desliga a conta — coluna Status da Administração de Contas.")
        .WithDescription(
            "Desativar encerra TODAS as sessões do usuário imediatamente (política §3.5). " +
            "É a exclusão do produto: não existe DELETE, porque apagar a conta quebraria as " +
            "referências de auditoria e das execuções que ela disparou.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        return app;
    }

    private static UserListItemDto Converter(User u) =>
        new(u.Id, u.Code, u.Username, u.Email, u.Role.ToString(), u.IsActive, u.CreatedAt);
}
