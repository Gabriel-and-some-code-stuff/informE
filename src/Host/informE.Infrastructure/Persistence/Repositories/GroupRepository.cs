using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class GroupRepository(AppDbContext db) : IGroupRepository
{
    public Task<Group?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Groups.FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<List<Group>> ListAsync(CancellationToken ct = default) =>
        db.Groups.ToListAsync(ct);

    public async Task AddAsync(Group group, CancellationToken ct = default) =>
        await db.Groups.AddAsync(group, ct);
}
