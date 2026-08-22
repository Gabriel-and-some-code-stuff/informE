using informE.Domain.Enums;

namespace informE.Application.Models;

// Entrada da RF09/RF10. Recebe a AÇÃO escolhida no dropdown, não um script — o
// script vem do MachineActionCatalog no servidor (RF14).
//
// "Em vez de Dispositivo de destino, colocar dispositivos OU grupo de destino":
// os dois podem vir juntos e o use case une os alvos, sem duplicar máquina que
// aparece nas duas listas.
public record DispatchTaskRequest(
    string Name,
    MachineActionKind Action,
    DateTimeOffset ScheduledAt,
    Guid CreatedByUserId,
    IReadOnlyCollection<Guid> TargetDeviceIds,
    IReadOnlyCollection<Guid>? TargetGroupIds = null
);
