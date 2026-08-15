using informE.Domain.Enums;

namespace informE.Application.Models;

// Entrada da RF09/RF10: "rodar este script nestes N dispositivos".
public record DispatchTaskRequest(
    string Name,
    string SourceScript,
    ScriptKind Kind,
    DateTimeOffset ScheduledAt,
    Guid CreatedByUserId,
    IReadOnlyCollection<Guid> TargetDeviceIds
);
