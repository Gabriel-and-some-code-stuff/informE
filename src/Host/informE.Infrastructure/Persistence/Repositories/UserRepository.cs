using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using informE.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    // Comparacao insensivel a caixa e sem espaco nas pontas.
    //
    // Era `u.Email == email`, exato: "Admin@cps.sp.gov.br" nao achava a conta
    // "admin@cps.sp.gov.br" e o login devolvia 401 na conta certa com a senha
    // certa. User.Normalizar cuida da escrita; aqui cuida da leitura, inclusive
    // das linhas gravadas antes desta correcao.
    //
    // ToLower() e nao EF.Functions.ILike: no ILike, `_` e `%` sao CURINGAS, e
    // e-mail pode conter `_` -- "a_min@x" casaria com "admin@x". Num caminho de
    // autenticacao isso e falha de seguranca, nao detalhe de conveniencia.
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizado = email.Trim().ToLowerInvariant();

        return db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizado, ct);
    }

    // Insensivel a caixa, pelo mesmo motivo do e-mail: "Carlos" e "carlos" sao
    // a mesma pessoa para quem digita, e deixar as duas contas existirem cria
    // ambiguidade que ninguem consegue resolver depois.
    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalizado = username.Trim().ToLowerInvariant();

        return db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalizado, ct);
    }

    // Filtros da tela de Administração de Contas — mesmo desenho do
    // DeviceRepository.ListAsync: cada filtro é opcional e só entra na query
    // quando vem preenchido.
    public Task<List<User>> ListAsync(UserRole? papel, bool? ativo, string? busca,
        CancellationToken ct = default)
    {
        var query = db.Users.AsQueryable();

        if (papel is not null)
            query = query.Where(u => u.Role == papel);

        if (ativo is not null)
            query = query.Where(u => u.IsActive == ativo);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            // ILIKE do Postgres: case-insensitive sem ToLower() dos dois lados
            // (que impediria o uso de índice).
            var padrao = $"%{busca.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.Username, padrao) ||
                EF.Functions.ILike(u.Email, padrao) ||
                EF.Functions.ILike(u.Code, padrao));
        }

        return query.OrderBy(u => u.Username).ToListAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);

    public async Task AddSessionAsync(Session session, CancellationToken ct = default) =>
        await db.Sessions.AddAsync(session, ct);

    public Task<List<Session>> GetActiveSessionsAsync(Guid userId, CancellationToken ct = default) =>
        db.Sessions.Where(s => s.UserId == userId && s.IsActive).ToListAsync(ct);

    public Task<Session?> GetSessionByIdAsync(Guid sessionId, CancellationToken ct = default) =>
        db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);

    // Bypass do Domain confirmado com o time -- update direto por Id, sem
    // carregar a entidade. Session.Revoke() fica sem uso neste caminho.
    public Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default) =>
        db.Sessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
}
