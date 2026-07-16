using informE.Domain.Entities;

namespace informE.Application.Abstractions.Repositories;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Group>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Group group, CancellationToken ct = default);
}
