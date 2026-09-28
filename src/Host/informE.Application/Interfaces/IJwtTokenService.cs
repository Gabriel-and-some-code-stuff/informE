using informE.Domain.Entities;

namespace informE.Application.Interfaces;

public interface IJwtTokenService
{
    // sessionId vira o claim `sid`. Sem ele o servidor recebe um token válido e
    // não sabe QUAL sessão ele representa — o que torna impossível fazer logout
    // de um dispositivo só, ou checar se aquela sessão foi revogada.
    string CreateAccessToken(User user, Guid sessionId);           // ~15 min
    (string Token, DateTimeOffset ExpiresAt) CreateRefreshToken(); // 7 dias, persistido
}
