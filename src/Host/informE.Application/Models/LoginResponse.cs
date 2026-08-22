using informE.Domain.Enums;

namespace informE.Application.Models;

// Role volta pro frontend decidir o que renderizar (o Viewer tem sidebar
// reduzida). A autorização de verdade continua no servidor, via claim do JWT —
// isto é só conveniência de UI.
public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid UserId,
    string Username,
    UserRole Role
);
