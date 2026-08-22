using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Contracts.Dtos;
using informE.Domain.Entities;

namespace informE.Application.UseCases;

// RF02 (dados atuais de CPU/RAM/disco, atualizar LastSeen) + RF04. Disparado
// pelo AgentHub a cada telemetria recebida — não existe RF de histórico aqui de
// propósito ("sem histórico", RF02); histórico diário (DeviceDailyMetrics) é uma
// feature separada, não este fluxo.
//
// RN03 (virar Offline após X minutos sem heartbeat) NÃO é responsabilidade
// desta use case — isso é a ausência de um evento, não a reação a um evento.
// Precisa de um BackgroundService varrendo Device.LastSeenAt periodicamente
// (mesmo padrão já citado em ARCHITECTURE.md §4 para sessões ociosas).
public class RecordDeviceHeartbeatUseCase(
    IDeviceRepository deviceRepository,
    IUnitOfWork unitOfWork,
    IDashboardNotifier dashboardNotifier)
{
    public async Task ExecuteAsync(TelemetryDto telemetry, CancellationToken ct = default)
    {
        var device = await deviceRepository.GetByIdAsync(telemetry.DeviceId, ct)
            ?? throw new InvalidOperationException(
                $"Device {telemetry.DeviceId} não encontrado — RF01 exige enroll antes do primeiro heartbeat.");

        // Regra de limiar vive no Domain (Device.EvaluateHealth), não aqui.
        var health = Device.EvaluateHealth(telemetry.CpuPercent, telemetry.RamPercent, telemetry.DiskPercent);

        device.MarkSeen(telemetry.Timestamp, health);

        await unitOfWork.SaveChangesAsync(ct);

        await dashboardNotifier.TelemetryAsync(telemetry, ct);
        await dashboardNotifier.DeviceStatusChangedAsync(device.Id, device.Status, device.Health, ct);
    }
}
