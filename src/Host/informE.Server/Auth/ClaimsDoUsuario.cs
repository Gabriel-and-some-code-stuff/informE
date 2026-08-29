using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using informE.Domain.Enums;

namespace informE.Server.Auth;

// Leitura dos claims do JWT em um lugar só.
//
// Antes isto vivia duplicado como método privado em ExecutionEndpoints e
// AgentEndpoints, com a mesma lógica de fallback `NameIdentifier ?? "sub"` —
// duas cópias que já divergiam na mensagem de erro.
public static class ClaimsDoUsuario
{
    // O `sub` do JWT é o Id do usuário — colocado lá pelo JwtTokenService.
    // O fallback existe porque o handler do ASP.NET mapeia `sub` para
    // ClaimTypes.NameIdentifier por padrão, mas isso pode ser desligado.
    public static Guid Id(this ClaimsPrincipal usuario)
    {
        var bruto = usuario.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? usuario.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(bruto, out var id)
            ? id
            : throw new UnauthorizedAccessException("Token sem identificação de usuário.");
    }

    public static UserRole Papel(this ClaimsPrincipal usuario)
    {
        var bruto = usuario.FindFirstValue(ClaimTypes.Role);

        return Enum.TryParse<UserRole>(bruto, ignoreCase: true, out var papel)
            ? papel
            : throw new UnauthorizedAccessException("Token sem papel identificado.");
    }

    // Claim `sid` — qual sessão este token representa. É o que permite ao
    // /auth/logout revogar só o dispositivo atual, e à tela de sessões marcar
    // qual linha é "este dispositivo".
    public static Guid? SessaoAtual(this ClaimsPrincipal usuario)
    {
        var bruto = usuario.FindFirstValue(JwtRegisteredClaimNames.Sid)
                 ?? usuario.FindFirstValue(ClaimTypes.Sid);

        return Guid.TryParse(bruto, out var id) ? id : null;
    }
}
