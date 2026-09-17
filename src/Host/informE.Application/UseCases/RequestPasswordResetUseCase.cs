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

    // Devolve o LINK, nao void.
    //
    // Quem decide expor o link e o Server, e so em Development (ver
    // AuthEndpoints): sem SMTP configurado nao existe outro jeito de a pessoa
    // receber o token, e a demonstracao precisa mostrar o fluxo funcionando.
    // Em producao o endpoint descarta este retorno e responde 204 seco.
    //
    // Devolve null quando nao ha conta -- e o mesmo silencio de antes.
    public async Task<string?> ExecuteAsync(string email, string urlBaseDeReset, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEmailAsync(email, ct);

        // Conta inexistente OU desativada: silêncio. Conta desativada não deve
        // conseguir voltar por reset de senha — quem reativa é o admin.
        if (user is null || !user.IsActive)
            return null;

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

        // O envio e MELHOR-ESFORCO e vem por ultimo, dentro de try.
        //
        // Antes ele podia lancar (SMTP nao configurado -> 409) DEPOIS de o token
        // ja estar salvo: sobrava token valido que ninguem recebeu, e a tela
        // recebia erro num pedido que na verdade deu certo. Falhar no envio nao
        // deve desfazer o pedido nem revelar nada a quem chamou.
        try
        {
            await emailSender.SendAsync(
                user.Email,
                "informE — redefinição de senha",
                $"""
                Olá, {user.Username}.

                Recebemos um pedido para redefinir sua senha no informE.

                Abra o link abaixo para escolher uma nova senha:
                {link}

                O link vale por 1 hora e só pode ser usado uma vez.
                Se não foi você, ignore esta mensagem.
                """,
                ct);
        }
        catch (Exception)
        {
            // Silencio de proposito: em desenvolvimento nao ha SMTP, e o link
            // volta pelo retorno. Em producao o operador ve a falha no log do
            // EmailSender, nao numa mensagem para quem pediu o reset.
        }

        return link;
    }

    // Base64 padrão tem '+', '/' e '=', que quebram em query string. Base64Url não.
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
