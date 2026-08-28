namespace informE.Contracts.Dtos.Api;

// Contratos REST entre Server e UI. Ficam em Contracts porque `informE.UI`
// referencia este projeto — assim a tela usa o mesmo tipo que o endpoint devolve,
// sem string mágica nem model duplicado.
//
// São records de LEITURA, achatados para a tela. Nunca devolvemos entidade do
// Domain direto: isso vazaria hash de senha, chave de agente e navegação circular.

// ── Autenticação ──────────────────────────────────────────────────────────────

public record LoginRequestDto(string Email, string Password);

public record LoginResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid UserId,
    string Username,
    string Role);

// ── Usuários ──────────────────────────────────────────────────────────────────

// "+ Novo Usuário" da tela de Administração de Contas. Role em texto: quem
// chama é o próprio front, que já sabe os valores do enum ("Viewer"/"Admin"/
// "SuperAdmin") — evita depender da ordem numérica do enum no JSON.
public record CreateUserRequestDto(string Username, string Email, string Password, string Role);

public record CreateUserResponseDto(Guid UserId);

// ── Equipamentos ──────────────────────────────────────────────────────────────

// Uma linha da tabela de Equipamentos. RAM/Disco/Uptime são nullable porque
// máquina offline não tem esses dados — a tela mostra "—".
public record DeviceListItemDto(
    Guid Id,
    string Hostname,
    string? GroupName,
    string LastIp,
    string Os,
    string Status,          // Conexão: Online/Offline/Unknown
    string Health,          // Saúde: Saudavel/Aviso/Critico/Erro
    string Role,            // Aluno/Professor
    int? UptimeSeconds,
    DateTimeOffset? LastSeenAt);

public record DeviceSummaryDto(int Total, int Online, int Offline, int ComProblema);

public record DeviceListResponseDto(DeviceSummaryDto Resumo, IReadOnlyList<DeviceListItemDto> Itens);

// ── Ações e execuções ─────────────────────────────────────────────────────────

// Alimenta o dropdown de Nova Execução.
public record MachineActionDto(string Kind, string DisplayName, string Description);

public record DispatchTaskRequestDto(
    string Action,
    IReadOnlyCollection<Guid> DeviceIds,
    IReadOnlyCollection<Guid>? GroupIds,
    DateTimeOffset? ScheduledAt); // null = executar agora

public record DispatchTaskResponseDto(Guid TaskId, string Code, int DispatchedCount);

// Uma linha da tabela de Execuções (grão = log, ou seja, por máquina).
public record ExecutionListItemDto(
    Guid LogId,
    Guid TaskId,
    string Code,            // EX-1000
    string DeviceHostname,
    string ActionName,
    string Status,
    DateTimeOffset ExecutedAt,
    int? DurationMs,
    string? Output);

// ── Agente ────────────────────────────────────────────────────────────────────

public record EnrollRequestDto(
    string EnrollmentToken,
    string Hostname,
    string IpAddress,
    string MacAddress,
    string Os,
    string OsUser,
    Guid? GroupId);

// AgentKey volta em texto claro UMA vez — o agente guarda com DPAPI.
public record EnrollResponseDto(Guid DeviceId, string AgentKey);

public record EnrollmentTokenResponseDto(string Token, DateTimeOffset ExpiresAt);
