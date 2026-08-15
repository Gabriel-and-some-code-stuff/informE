using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);

    public async Task AddSessionAsync(Session session, CancellationToken ct = default) =>
        await db.Sessions.AddAsync(session, ct);

    public Task<List<Session>> GetActiveSessionsAsync(Guid userId, CancellationToken ct = default) =>
        db.Sessions.Where(s => s.UserId == userId && s.IsActive).ToListAsync(ct);

    // Bypass do Domain confirmado com o time -- update direto por Id, sem
    // carregar a entidade. Session.Revoke() fica sem uso neste caminho.
    public Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default) =>
        db.Sessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
}
