using informE.Domain.Enums;
using System.Security.Claims;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Contracts.Dtos.Api;
using informE.Domain.Entities;
using informE.Server.Auth;

namespace informE.Server.Endpoints;

// Os grupos (laboratórios) eram um beco sem saída na API: DispatchTaskRequestDto
// aceita GroupIds, mas nenhuma rota devolvia esses Ids. Quem usasse o Scalar não
// tinha como disparar uma ação em um laboratório inteiro.
public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/groups").WithTags("Grupos").RequireAuthorization();

        grupo.MapGet("/", async (
            IGroupRepository repositorio,
            ClaimsPrincipal usuario,
            CancellationToken ct) =>
        {
            // Viewer ve APENAS os laboratorios que sao dele.
            //
            // O escopo e decidido AQUI, no servidor, e nao por um filtro que a
            // tela manda. Se o recorte dependesse de parametro, bastaria omiti-lo
            // para um Viewer enxergar o parque inteiro -- a politica
            // (docs/politica-login-sessao.md §1) diz "acesso apenas aos proprios
            // dados/maquinas", e isso e uma regra de autorizacao, nao de tela.
            var grupos = usuario.Papel() == UserRole.Viewer
                ? await repositorio.ListByOwnerAsync(usuario.Id(), ct)
                : await repositorio.ListAsync(ct);

            var itens = grupos
                .Select(g => new GroupListItemDto(g.Id, g.Name, g.Description, g.Devices.Count))
                .ToList();

            return Results.Ok(itens);
        })
        .WithName("ListarGrupos")
        .WithSummary("Laboratórios visíveis para quem chamou, e quantas máquinas cada um tem.")
        .WithDescription(
            "Alimenta a tela de Grupos e o seletor \"dispositivos ou grupo de destino\" da tela de " +
            "Nova Execução. Para o detalhe de um grupo, use GET /devices?grupoId=. " +
            "ESCOPO: Admin e SuperAdmin veem todos os laboratórios; Viewer vê apenas os " +
            "que são dele (Group.OwnerId). O recorte é do servidor, não da tela.")
        .Produces<List<GroupListItemDto>>();

        // ponytail: sem use case. Criar grupo nao tem regra de negocio nenhuma
        // alem de "quem cria e o dono" — uma classe de caso de uso aqui seria uma
        // linha de logica embrulhada em cinco de cerimonia. Se um dia entrar
        // regra (limite por instancia, nome unico por escola), ai sim promove.
        grupo.MapPost("/", async (
            CreateGroupRequestDto corpo,
            ClaimsPrincipal usuario,
            IGroupRepository repositorio,
            IUnitOfWork unitOfWork,
            CancellationToken ct) =>
        {
            // O construtor de Group valida nome e descrição e lança se inválido.
            var novo = new Group(corpo.Name, corpo.Description, usuario.Id())
            {
                Id = Guid.NewGuid() // precisa existir antes do save: vai na resposta
            };

            await repositorio.AddAsync(novo, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return Results.Ok(new CreateGroupResponseDto(novo.Id));
        })
        .WithName("CriarGrupo")
        .WithSummary("Cria um laboratório.")
        .WithDescription("Quem cria vira o dono (Group.OwnerId). Nome até 45 caracteres, descrição até 100.")
        .Produces<CreateGroupResponseDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

        return app;
    }
}
