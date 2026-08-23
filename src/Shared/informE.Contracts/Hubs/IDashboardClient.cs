using informE.Contracts.Dtos;

namespace informE.Contracts.Hubs;

// Métodos que o Server invoca nos operadores (via DashboardHub).
public interface IDashboardClient
{
    // Conexão e saúde são colunas separadas na tela de Equipamentos e mudam no
    // mesmo heartbeat — viajam juntas. Enums como string pro cliente não precisar
    // conhecer o Domain.
    Task EndpointStatusChanged(Guid deviceId, string status, string health);
    Task TelemetryUpdated(TelemetryDto telemetry);
    Task AlertRaised(AlertDto alert);
    Task TaskProgress(Guid taskId, string status);
}
