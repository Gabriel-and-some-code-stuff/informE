using informE.Application.Interfaces;
using informE.Contracts.Dtos;
using informE.Contracts.Hubs;
using informE.Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using TaskStatus = informE.Domain.Enums.TaskStatus;

namespace informE.Infrastructure.Realtime;

// Implementa o port IDashboardNotifier empurrando pelo DashboardHub.
//
// ponytail: broadcast pra todos os operadores conectados. Filtrar por grupo
// (o Viewer vê só o Grupo 3) exige Groups do SignalR + o escopo N-N
// Admin↔Group, que é Fase 2 — ver docs/politica-login-sessao.md.
public class SignalRDashboardNotifier(IHubContext<DashboardHub, IDashboardClient> hub) : IDashboardNotifier
{
    public Task TelemetryAsync(TelemetryDto telemetry, CancellationToken ct = default) =>
        hub.Clients.All.TelemetryUpdated(telemetry);

    public Task AlertAsync(AlertDto alert, CancellationToken ct = default) =>
        hub.Clients.All.AlertRaised(alert);

    public Task DeviceStatusChangedAsync(Guid deviceId, EndpointStatus status, HealthStatus health, CancellationToken ct = default) =>
        hub.Clients.All.EndpointStatusChanged(deviceId, status.ToString(), health.ToString());

    public Task TaskProgressAsync(Guid taskId, TaskStatus status, CancellationToken ct = default) =>
        hub.Clients.All.TaskProgress(taskId, status.ToString());
}
