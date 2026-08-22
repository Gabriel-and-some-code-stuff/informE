using informE.Contracts.Dtos;
using informE.Domain.Enums;

namespace informE.Application.Interfaces;

// Empurra eventos ao vivo para operadores via DashboardHub (SignalR).
public interface IDashboardNotifier
{
    Task TelemetryAsync(TelemetryDto telemetry, CancellationToken ct = default);
    Task AlertAsync(AlertDto alert, CancellationToken ct = default);
    // Conexão e saúde são colunas separadas na tela de Equipamentos — as duas
    // mudam no mesmo heartbeat, então viajam no mesmo evento.
    Task DeviceStatusChangedAsync(Guid deviceId, EndpointStatus status, HealthStatus health, CancellationToken ct = default);
    Task TaskProgressAsync(Guid taskId, Domain.Enums.TaskStatus status, CancellationToken ct = default);
}
