using informE.Domain.Entities;

namespace informE.Application.Interfaces.Repositories;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken ct = default);

    // Busca pelo Id (que vem no link do e-mail). O segredo é verificado depois,
    // contra o hash — não dá pra buscar pelo hash, o Argon2 tem salt aleatório.
    Task<PasswordResetToken?> GetByIdAsync(Guid id, CancellationToken ct = default);

    // Invalida os tokens ainda válidos do usuário antes de emitir um novo, pra
    // não deixar vários links de reset vivos ao mesmo tempo.
    Task InvalidateActiveForUserAsync(Guid userId, CancellationToken ct = default);
}
