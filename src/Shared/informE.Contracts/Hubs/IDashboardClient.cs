using informE.Contracts.Dtos;

namespace informE.Contracts.Hubs;

// Métodos que o Server invoca nos operadores (via DashboardHub).
public interface IDashboardClient
{
    Task EndpointStatusChanged(Guid deviceId, string status); // EndpointStatus como string
    Task TelemetryUpdated(TelemetryDto telemetry);
    Task AlertRaised(AlertDto alert);
    Task TaskProgress(Guid taskId, string status);            // TaskStatus como string
}
