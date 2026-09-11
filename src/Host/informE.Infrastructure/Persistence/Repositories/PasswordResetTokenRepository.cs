using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class PasswordResetTokenRepository(AppDbContext db) : IPasswordResetTokenRepository
{
    public async Task AddAsync(PasswordResetToken token, CancellationToken ct = default) =>
        await db.PasswordResetTokens.AddAsync(token, ct);

    public Task<PasswordResetToken?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.PasswordResetTokens.FirstOrDefaultAsync(t => t.Id == id, ct);

    // Marca como usados os que ainda valiam. ExecuteUpdate em vez de carregar as
    // entidades: é update em bloco por condição, não mutação de agregado.
    public Task InvalidateActiveForUserAsync(Guid userId, CancellationToken ct = default) =>
        db.PasswordResetTokens
            .Where(t => t.UserId == userId && !t.IsUsed)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.IsUsed, true), ct);
}
