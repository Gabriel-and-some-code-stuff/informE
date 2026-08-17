using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Contracts.Dtos;

namespace informE.Application.UseCases;

// RF02 (dados atuais de CPU/RAM/disco, atualizar LastSeen) + RF04 (estados
// Online/Degraded). Disparado pelo AgentHub a cada telemetria recebida — não
// existe RF de histórico aqui de propósito ("sem histórico", RF02); histórico
// diário (DeviceDailyMetrics) é uma feature separada, não este fluxo.
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
    // ponytail: limiar único e arbitrário pra todos os devices — nenhum RF/RN
    // define o que caracteriza "Degraded". Time deve validar ou parametrizar
    // por device/grupo antes de confiar nisso em produção.
    private const float DegradedThresholdPercent = 90f;

    public async Task ExecuteAsync(TelemetryDto telemetry, CancellationToken ct = default)
    {
        var device = await deviceRepository.GetByIdAsync(telemetry.DeviceId, ct)
            ?? throw new InvalidOperationException(
                $"Device {telemetry.DeviceId} não encontrado — RF01 exige enroll antes do primeiro heartbeat.");

        var isDegraded = telemetry.CpuPercent > DegradedThresholdPercent
            || telemetry.RamPercent > DegradedThresholdPercent
            || telemetry.DiskPercent > DegradedThresholdPercent;

        if (isDegraded)
            device.MarkDegraded(telemetry.Timestamp);
        else
            device.MarkSeen(telemetry.Timestamp);

        await unitOfWork.SaveChangesAsync(ct);

        await dashboardNotifier.TelemetryAsync(telemetry, ct);
        await dashboardNotifier.DeviceStatusChangedAsync(device.Id, device.Status, ct);
    }
}
