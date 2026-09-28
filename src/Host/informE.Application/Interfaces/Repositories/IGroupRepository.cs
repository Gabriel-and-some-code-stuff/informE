using informE.Domain.Entities;

namespace informE.Application.Interfaces.Repositories;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Group>> ListAsync(CancellationToken ct = default);

    // Grupos de um dono. E assim que o Viewer descobre o proprio laboratorio:
    // Group.OwnerId ja e 1-para-1 com um User, entao nao precisa de campo novo
    // no usuario nem de migracao -- ver docs/politica-login-sessao.md §1, que
    // classificou escopo granular por Group como Fase 2 justamente porque
    // "Admin escopado a varios grupos" exigiria uma associacao N-N. Um Viewer
    // com UM laboratorio cabe no modelo de hoje.
    Task<List<Group>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task AddAsync(Group group, CancellationToken ct = default);
}
