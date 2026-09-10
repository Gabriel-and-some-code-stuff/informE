using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class GroupRepository(AppDbContext db) : IGroupRepository
{
    public Task<Group?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Groups.FirstOrDefaultAsync(g => g.Id == id, ct);

    // Include dos devices: a tela de Grupos mostra "N máquinas" por card, e sem
    // isso a contagem viria zerada.
    //
    // ponytail: carrega as entidades inteiras para contar. Com 5 grupos x 21
    // maquinas isso e irrelevante; se o parque crescer para milhares, troque por
    // uma projecao com GroupBy/Count no banco.
    public Task<List<Group>> ListAsync(CancellationToken ct = default) =>
        db.Groups.Include(g => g.Devices).OrderBy(g => g.Name).ToListAsync(ct);

    public async Task AddAsync(Group group, CancellationToken ct = default) =>
        await db.Groups.AddAsync(group, ct);

    public Task<List<Group>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
        db.Groups
            .Include(g => g.Devices)
            .Where(g => g.OwnerId == ownerId)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);
}
