namespace informE.Contracts.Dtos;

public record TelemetryDto(
    Guid DeviceId,
    float CpuPercent,
    float RamPercent,
    float DiskPercent,
    DateTimeOffset Timestamp
);
