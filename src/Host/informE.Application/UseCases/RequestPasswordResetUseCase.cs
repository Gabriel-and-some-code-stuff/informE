using System.Security.Cryptography;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;

namespace informE.Application.UseCases;

// Tela "Esqueci a senha" (comentário #227). Manda link de redefinição pro e-mail
// institucional do usuário — o mesmo do cadastro.
//
// NÃO devolve erro quando o e-mail não existe: a resposta é sempre a mesma
// ("se este e-mail estiver cadastrado, você receberá um link"). Diferenciar
// permitiria descobrir quais e-mails têm conta, que é o mesmo raciocínio do
// InvalidCredentialsException no LoginUseCase.
public class RequestPasswordResetUseCase(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork)
{
    private const int TamanhoDoSegredoEmBytes = 32;

    public async Task ExecuteAsync(string email, string urlBaseDeReset, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEmailAsync(email, ct);

        // Conta inexistente OU desativada: silêncio. Conta desativada não deve
        // conseguir voltar por reset de senha — quem reativa é o admin.
        if (user is null || !user.IsActive)
            return;

        // Um link válido por vez: pedir de novo mata o anterior.
        await tokenRepository.InvalidateActiveForUserAsync(user.Id, ct);

        var segredo = Base64Url(RandomNumberGenerator.GetBytes(TamanhoDoSegredoEmBytes));

        var token = new PasswordResetToken(user.Id, passwordHasher.Hash(segredo))
        {
            Id = Guid.NewGuid() // precisa existir antes do save: vai no link
        };

        await tokenRepository.AddAsync(token, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // "{Id}.{segredo}": o Id acha a linha, o segredo prova que é o dono do e-mail.
        var link = $"{urlBaseDeReset.TrimEnd('/')}?token={token.Id}.{segredo}";

        await emailSender.SendAsync(
            user.Email,
            "informE — redefinição de senha",
            $"""
            Olá, {user.Username}.

            Recebemos um pedido para redefinir sua senha no informE.

            Abra o link abaixo para escolher uma nova senha:
            {link}

            O link vale por 1 hora e só pode ser usado uma vez.

            Se não foi você que pediu, ignore este e-mail — sua senha continua a mesma.
            """,
            ct);
    }

    // Base64 padrão tem '+', '/' e '=', que quebram em query string. Base64Url não.
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
