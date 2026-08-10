using informE.Domain.Entities;

namespace informE.Application.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct = default);
    Task<List<AuditLog>> ListAsync(int page, int pageSize, CancellationToken ct = default);
}
