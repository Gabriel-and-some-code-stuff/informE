using informE.Domain.Entities;

namespace informE.Application.Interfaces;

public interface IJwtTokenService
{
    string CreateAccessToken(User user);                           // ~15 min
    (string Token, DateTimeOffset ExpiresAt) CreateRefreshToken(); // 7 dias, persistido
}
