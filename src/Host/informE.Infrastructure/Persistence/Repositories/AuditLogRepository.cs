using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class AuditLogRepository(AppDbContext db) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog log, CancellationToken ct = default) =>
        await db.AuditLogs.AddAsync(log, ct);

    // Mais recente primeiro — alimenta "Atividade Recente" na tela de Meu Perfil.
    public Task<List<AuditLog>> ListAsync(int page, int pageSize, CancellationToken ct = default) =>
        db.AuditLogs
            .OrderByDescending(l => l.CreatedAt)
            .Skip(Math.Max(page - 1, 0) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
}
