using informE.Contracts.Dtos;
using informE.Domain.Enums;

namespace informE.Application.Abstractions;

// Empurra eventos ao vivo para operadores via DashboardHub (SignalR).
public interface IDashboardNotifier
{
    Task TelemetryAsync(TelemetryDto telemetry, CancellationToken ct = default);
    Task AlertAsync(AlertDto alert, CancellationToken ct = default);
    Task DeviceStatusChangedAsync(Guid deviceId, EndpointStatus status, CancellationToken ct = default);
    Task TaskProgressAsync(Guid taskId, Domain.Enums.TaskStatus status, CancellationToken ct = default);
}
