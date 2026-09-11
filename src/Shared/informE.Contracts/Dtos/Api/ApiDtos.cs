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

// O refresh token viaja no corpo, não em header: é credencial de longa duração e
// não deve aparecer em log de acesso nem em histórico de URL.
public record RefreshRequestDto(string RefreshToken);

public record ForgotPasswordRequestDto(string Email);

public record ResetPasswordRequestDto(string Token, string NovaSenha);

// ── Usuários ──────────────────────────────────────────────────────────────────

// "+ Novo Usuário" da tela de Administração de Contas. Role em texto: quem
// chama é o próprio front, que já sabe os valores do enum ("Viewer"/"Admin"/
// "SuperAdmin") — evita depender da ordem numérica do enum no JSON.
public record CreateUserRequestDto(string Username, string Email, string Password, string Role);

public record CreateUserResponseDto(Guid UserId);

// Uma linha da tabela de Administração de Contas. `Code` é o rótulo humano
// (USR-0001) gerado por sequence do Postgres; o Guid continua sendo a chave.
public record UserListItemDto(
    Guid Id,
    string Code,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt);

public record UpdateUserRequestDto(string? Username, string? Email);

public record ChangeRoleRequestDto(string Role);

public record SetActiveRequestDto(bool Ativo);

// Painel "Sessões ativas" de Meu Perfil (comentário #241 do Figma: IP da máquina
// + quando o último login foi feito).
public record SessionListItemDto(
    Guid Id,
    string? DeviceLabel,
    string IpAddress,
    DateTimeOffset LoginAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    bool EhSessaoAtual);

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
    DateTimeOffset? LastSeenAt,
    // Percentuais do último snapshot. Null em máquina offline ou que nunca
    // reportou — a tela mostra "—", nunca 0%, que seria uma leitura falsa.
    float? CpuPercent = null,
    float? RamPercent = null,
    float? DiskPercent = null);

// Detalhe do equipamento: a linha da lista + o hardware da tabela info_devices.
//
// Todo o bloco de hardware é nullable de propósito. O agente coleta APENAS
// CPU/RAM/disco/uptime (ver SystemSnapshotCollector) — modelo de processador,
// GPU, placa-mãe e BIOS não são coletados, decisão registrada em
// docs/pendencias-front-auditoria.md §1. As máquinas do seed têm esses dados;
// a máquina real vem null e a tela deve exibir "Não disponível".
public record DeviceDetailDto(
    DeviceListItemDto Equipamento,
    HardwareDto? Hardware);

public record HardwareDto(
    string? Cpu,
    string? Gpu,
    int? RamGb,
    string? RamType,
    int? StorageGb,
    string? StorageType,
    string? MotherBoard,
    string? Bios,
    DateTimeOffset? CollectedAt);

public record DeviceSummaryDto(int Total, int Online, int Offline, int ComProblema);

public record DeviceListResponseDto(DeviceSummaryDto Resumo, IReadOnlyList<DeviceListItemDto> Itens);

// ── Grupos (laboratórios) ─────────────────────────────────────────────────────

// Alimenta a tela de Grupos e o seletor "dispositivos ou grupo de destino" da
// tela de Nova Execução — que hoje exige GroupIds sem oferecer de onde tirá-los.
public record GroupListItemDto(Guid Id, string Name, string? Description, int TotalDeDispositivos);

public record CreateGroupRequestDto(string Name, string? Description);

public record CreateGroupResponseDto(Guid GroupId);

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
//
// `CriadoPor` responde "quem mandou isso?" — item 8 do documento de pendências.
// MachineTask guarda CreatedByUserId, mas sem propriedade de navegação para
// User, então o nome é resolvido por lookup no endpoint. Vem null quando o
// usuário foi removido depois de disparar a execução; a tela mostra "—".
public record ExecutionListItemDto(
    Guid LogId,
    Guid TaskId,
    string Code,            // EX-1000
    string DeviceHostname,
    string ActionName,
    string Status,
    DateTimeOffset ExecutedAt,
    int? DurationMs,
    string? Output,
    string? CriadoPor = null);

// Detalhe de uma execução: o status da TAREFA mais as linhas por máquina. A
// separação importa — a tarefa só fecha quando nenhum log está mais pendente.
public record TaskDetailDto(Guid Id, string Code, string Status, IReadOnlyList<ExecutionListItemDto> Maquinas);

// ── Alertas ───────────────────────────────────────────────────────────────────

// Uma linha do painel "Alertas Recentes".
//
// `Categoria` e derivada em tempo de leitura por AlertCategoryMap: os 11
// AlertType tecnicos colapsam nas 6 faixas do grafico do Figma. Nao existe
// coluna de categoria no banco — e agrupamento de apresentacao.
//
// NAO existe campo de severidade no dominio. O documento de pendencias pede
// "filtrar por categoria e severidade"; categoria existe, severidade nao —
// ver docs/pendencias-front-auditoria.md item 5.
public record AlertListItemDto(
    Guid Id,
    Guid DeviceId,
    string DeviceHostname,
    string? GroupName,
    string Tipo,          // AlertType tecnico (HighCpu, DiskFull, ...)
    string Categoria,     // Faixa de apresentacao (Hardware, Armazenamento, ...)
    string? Mensagem,
    DateTimeOffset OcorridoEm);

// Uma barra do grafico "Historico de Alertas" (stacked bar por dia).
// `PorCategoria` traz as 6 faixas sempre presentes, inclusive com zero, para a
// tela nao precisar completar dia vazio.
public record AlertHistoryDayDto(
    DateOnly Dia,
    int Total,
    IReadOnlyDictionary<string, int> PorCategoria);

public record AlertsResponseDto(
    int Total,
    IReadOnlyDictionary<string, int> PorCategoria,
    IReadOnlyList<AlertHistoryDayDto> Historico,
    IReadOnlyList<AlertListItemDto> Recentes);

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
