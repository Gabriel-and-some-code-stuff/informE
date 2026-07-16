namespace informE.Contracts.Dtos;

public record AlertDto(
    Guid DeviceId,
    string AlertType,  // valor do enum AlertType como string
    string Message,
    DateTimeOffset Timestamp
);
