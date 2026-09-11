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
            // Login do MESMO dispositivo é substituição, não dispositivo novo.
            //
            // Sem isto o limite conta SESSÕES, não dispositivos: fechar o navegador
            // e entrar de novo três vezes trancava o usuário fora da própria conta
            // sem ele nunca ter usado mais de uma máquina. A política fala em "3
            // dispositivos" (docs/politica-login-sessao.md §2.1) — é o DeviceLabel
            // que decide, não a contagem de linhas.
            var doMesmoDispositivo = vigentes
                .Where(s => s.DeviceLabel == request.DeviceLabel)
                .ToList();

            foreach (var anterior in doMesmoDispositivo)
                anterior.Revoke();

            var outrosDispositivos = vigentes.Count - doMesmoDispositivo.Count;

            if (outrosDispositivos >= LimiteDeSessoesPrivilegiadas)
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

        var (segredo, refreshExpiresAt) = jwtTokenService.CreateRefreshToken();

        // Só o HASH do refresh token vai pro banco — mesmo tratamento de senha.
        var session = new Session(
            request.IpAddress,
            refreshExpiresAt,
            passwordHasher.Hash(segredo),
            user.Id,
            request.DeviceLabel)
        {
            // Id no cliente, não pelo gen_random_uuid(): ele entra no token que
            // devolvemos abaixo, então precisa existir antes do SaveChanges.
            Id = Guid.NewGuid()
        };

        var accessToken = jwtTokenService.CreateAccessToken(user, session.Id);

        await userRepository.AddSessionAsync(session, ct);
        await RegistrarAuditoria("login_ok", request.IpAddress, user.Id, ct);

        await unitOfWork.SaveChangesAsync(ct);

        // "{sessionId}.{segredo}", mesmo formato do PasswordResetToken e pelo
        // mesmo motivo: o hash é Argon2id com salt aleatório, então não existe
        // `WHERE refresh_token_hash = ?`. O Id acha a linha, o segredo prova
        // que quem apresenta o token é o dono dela.
        var refreshToken = $"{session.Id}.{segredo}";

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
