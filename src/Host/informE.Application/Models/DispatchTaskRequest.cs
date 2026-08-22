using informE.Domain.Enums;

namespace informE.Application.Models;

// Entrada da RF09/RF10: "rodar esta ação nestes N dispositivos".
// Recebe a AÇÃO escolhida no dropdown, não um script — o script vem do
// MachineActionCatalog no servidor (RF14).
public record DispatchTaskRequest(
    string Name,
    MachineActionKind Action,
    DateTimeOffset ScheduledAt,
    Guid CreatedByUserId,
    IReadOnlyCollection<Guid> TargetDeviceIds
);
