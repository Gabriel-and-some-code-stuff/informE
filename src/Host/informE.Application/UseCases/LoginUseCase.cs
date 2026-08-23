using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Domain.Entities;
using informE.Domain.Enums;

namespace informE.Application.UseCases;

// Tela de Login (Administrador / Viewer). Aplica a política de sessão de
// docs/politica-login-sessao.md.
public class LoginUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork)
{
    // política §2.1: Admin/SuperAdmin podem ter 3 dispositivos ativos.
    private const int LimiteDeSessoesPrivilegiadas = 3;

    public async Task<LoginResponse> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, ct);

        // Usuário inexistente e senha errada caem na MESMA exceção de propósito —
        // distinguir permitiria enumerar e-mails cadastrados.
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await RegistrarAuditoria("login_failed", request.IpAddress, user?.Id, ct);
            throw new InvalidCredentialsException();
        }

        if (!user.IsActive)
        {
            await RegistrarAuditoria("login_inactive", request.IpAddress, user.Id, ct);
            throw new AccountDisabledException();
        }

        var sessoesAtivas = await userRepository.GetActiveSessionsAsync(user.Id, ct);

        // Sessão expirada por tempo ainda vem marcada IsActive=true no banco (a
        // varredura é de um BackgroundService que ainda não existe), então não
        // pode contar para o limite de dispositivos.
        var vigentes = sessoesAtivas.Where(s => !s.IsExpired()).ToList();

        // política §2.1 vs §2.2: privilegiado BLOQUEIA o 4º; Viewer é sessão única
        // com kick automático da anterior.
        if (user.Role is UserRole.Admin or UserRole.SuperAdmin)
        {
            if (vigentes.Count >= LimiteDeSessoesPrivilegiadas)
            {
                await RegistrarAuditoria("login_blocked", request.IpAddress, user.Id, ct);
                throw new DeviceLimitReachedException(LimiteDeSessoesPrivilegiadas);
            }
        }
        else
        {
            // ponytail: kick incondicional. A política prevê sessões simultâneas
            // para professor (contexto escola) vs. sessão única para funcionário
            // (contexto empresa), mas "tipo da instância" é config que ainda não
            // existe — ver docs/politica-login-sessao.md §2.3.
            foreach (var anterior in vigentes)
            {
                anterior.Revoke();
                await RegistrarAuditoria("session_kicked", request.IpAddress, user.Id, ct);
            }
        }

        var accessToken = jwtTokenService.CreateAccessToken(user);
        var (refreshToken, refreshExpiresAt) = jwtTokenService.CreateRefreshToken();

        // Só o HASH do refresh token vai pro banco — mesmo tratamento de senha.
        var session = new Session(
            request.IpAddress,
            refreshExpiresAt,
            passwordHasher.Hash(refreshToken),
            user.Id,
            request.DeviceLabel);

        await userRepository.AddSessionAsync(session, ct);
        await RegistrarAuditoria("login_ok", request.IpAddress, user.Id, ct);

        await unitOfWork.SaveChangesAsync(ct);

        return new LoginResponse(accessToken, refreshToken, refreshExpiresAt, user.Id, user.Username, user.Role);
    }

    // AuditLog.Action descarta silenciosamente strings com 30+ caracteres
    // (AuditLog.cs:26) — todas as ações aqui são curtas de propósito.
    private async Task RegistrarAuditoria(string acao, string ipAddress, Guid? userId, CancellationToken ct)
    {
        if (userId is null)
            return; // AuditLog exige UserId; tentativa em e-mail inexistente não tem a quem atribuir

        await auditLogRepository.AddAsync(new AuditLog(acao, ipAddress, userId.Value), ct);
    }
}
