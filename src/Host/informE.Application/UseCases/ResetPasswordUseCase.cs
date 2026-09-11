using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;

namespace informE.Application.UseCases;

// Segunda metade do "esqueci a senha": o usuário abriu o link e escolheu a nova
// senha.
public class ResetPasswordUseCase(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(string token, string novaSenha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(novaSenha))
            throw new ArgumentException("A nova senha não pode ser vazia.");

        var (id, segredo) = QuebrarToken(token);

        var resetToken = await tokenRepository.GetByIdAsync(id, ct);

        // Inexistente, expirado, usado ou segredo errado — tudo dá a mesma exceção.
        if (resetToken is null || !resetToken.IsValid() || !passwordHasher.Verify(segredo, resetToken.TokenHash))
            throw new InvalidResetTokenException();

        var user = await userRepository.GetByIdAsync(resetToken.UserId, ct)
            ?? throw new InvalidResetTokenException();

        user.ChangePassword(passwordHasher.Hash(novaSenha));
        resetToken.Redeem();

        // Senha trocada derruba todas as sessões: se alguém entrou com a senha
        // antiga, perde o acesso agora. É o cenário que motiva o reset.
        //
        // ⚠️ Só mata os refresh tokens. O access token é stateless (15 min), então
        // uma sessão invadida ainda funciona até ele vencer — limitação conhecida
        // de JWT sem lista de revogação.
        var sessoes = await userRepository.GetActiveSessionsAsync(user.Id, ct);
        foreach (var sessao in sessoes)
            sessao.Revoke();

        await unitOfWork.SaveChangesAsync(ct);
    }

    private static (Guid Id, string Segredo) QuebrarToken(string token)
    {
        var partes = (token ?? string.Empty).Split('.', 2);

        if (partes.Length != 2 || !Guid.TryParse(partes[0], out var id) || string.IsNullOrWhiteSpace(partes[1]))
            throw new InvalidResetTokenException();

        return (id, partes[1]);
    }
}
