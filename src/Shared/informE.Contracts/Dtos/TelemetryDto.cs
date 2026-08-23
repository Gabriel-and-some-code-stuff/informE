namespace informE.Contracts.Dtos;

// Snapshot do estado atual da máquina. O agente manda um ao abrir e depois a
// cada 30 min — não é stream contínuo. Serve de heartbeat (RF07) e alimenta as
// colunas Conexão/Saúde/Uptime da tela de Equipamentos.
public record TelemetryDto(
    Guid DeviceId,
    float CpuPercent,
    float RamPercent,
    float DiskPercent,
    int UptimeSeconds, // desde o boot da máquina; a tela formata como "3d 12h"
    DateTimeOffset Timestamp
);
