using informE.Domain.Entities;
using informE.Domain.Enums;

namespace informE.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    // `users.username` tem indice UNICO. Sem esta busca, criar um usuario com
    // nome repetido chegava no banco, violava ix_users_username (SqlState
    // 23505) e virava 500 "Erro interno. Consulte os logs do servidor." -- para
    // quem esta na tela, um erro sem explicacao no lugar de "esse nome ja
    // esta em uso".
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);

    // Tela de Administração de Contas. Mesmos filtros opcionais do
    // IDeviceRepository.ListAsync — cada um só entra na query se vier preenchido.
    Task<List<User>> ListAsync(UserRole? papel, bool? ativo, string? busca, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task AddSessionAsync(Session session, CancellationToken ct = default);
    Task<List<Session>> GetActiveSessionsAsync(Guid userId, CancellationToken ct = default);

    // Necessário pra revogar UMA sessão específica ("desconectar sessão/dispositivo,
    // em cada um deles") verificando de quem ela é antes.
    Task<Session?> GetSessionByIdAsync(Guid sessionId, CancellationToken ct = default);

    Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default);
}
