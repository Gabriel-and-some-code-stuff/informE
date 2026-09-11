using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;

namespace informE.Application.UseCases;

// POST /auth/refresh — troca um refresh token válido por um access token novo.
//
// Até aqui o refresh token era emitido, hasheado e persistido, e NADA o consumia:
// quando o access token de 15 min vencia, o usuário simplesmente tinha que logar
// de novo. Era a lacuna mais visível da API (docs/api-server.md).
//
// ROTAÇÃO: cada refresh gera um segredo novo e sobrescreve o hash. O token
// apresentado deixa de valer no mesmo instante, então um refresh token que vazou
// só serve enquanto o dono legítimo não usar o dele — e quando usar, o do
// atacante morre (e vice-versa, o que torna o roubo detectável pelo logout
// inesperado).
public class RefreshTokenUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork)
{
    public async Task<LoginResponse> ExecuteAsync(string refreshToken, CancellationToken ct = default)
    {
        var (sessionId, segredo) = QuebrarToken(refreshToken);

        var session = await userRepository.GetSessionByIdAsync(sessionId, ct);

        // Sessão inexistente, revogada (logout, kick, troca de papel, reset de
        // senha) ou vencida — tudo dá a mesma exceção.
        if (session is null || !session.IsActive || session.IsExpired())
            throw new InvalidRefreshTokenException();

        if (!passwordHasher.Verify(segredo, session.RefreshTokenHash))
            throw new InvalidRefreshTokenException();

        var user = await userRepository.GetByIdAsync(session.UserId, ct)
            ?? throw new InvalidRefreshTokenException();

        // Conta desativada no meio da sessão não renova. Sem esta checagem, quem
        // fosse bloqueado continuaria entrando por 7 dias no refresh token.
        if (!user.IsActive)
            throw new AccountDisabledException();

        var (novoSegredo, novaValidade) = jwtTokenService.CreateRefreshToken();

        session.RotateRefreshToken(passwordHasher.Hash(novoSegredo), novaValidade);

        // O access token novo carrega o papel ATUAL do usuário — é por isso que
        // ChangeUserRoleUseCase revoga as sessões: senão um rebaixado renovaria
        // indefinidamente um token com o papel antigo.
        var accessToken = jwtTokenService.CreateAccessToken(user, session.Id);

        await unitOfWork.SaveChangesAsync(ct);

        return new LoginResponse(
            accessToken,
            $"{session.Id}.{novoSegredo}",
            novaValidade,
            user.Id,
            user.Username,
            user.Role);
    }

    // Mesmo formato do ResetPasswordUseCase: "{Guid}.{segredo}".
    private static (Guid SessionId, string Segredo) QuebrarToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidRefreshTokenException();

        var separador = token.IndexOf('.');

        // IndexOf e não Split: o segredo é Base64 e pode conter '.'? Não pode,
        // mas depender disso seria frágil — cortar no PRIMEIRO ponto é exato.
        if (separador <= 0 || separador == token.Length - 1)
            throw new InvalidRefreshTokenException();

        if (!Guid.TryParse(token[..separador], out var sessionId))
            throw new InvalidRefreshTokenException();

        return (sessionId, token[(separador + 1)..]);
    }
}
