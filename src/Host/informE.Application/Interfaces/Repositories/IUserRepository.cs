using informE.Domain.Entities;

namespace informE.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task AddSessionAsync(Session session, CancellationToken ct = default);
    Task<List<Session>> GetActiveSessionsAsync(Guid userId, CancellationToken ct = default);

    // Necessário pra revogar UMA sessão específica ("desconectar sessão/dispositivo,
    // em cada um deles") verificando de quem ela é antes.
    Task<Session?> GetSessionByIdAsync(Guid sessionId, CancellationToken ct = default);

    Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default);
}
