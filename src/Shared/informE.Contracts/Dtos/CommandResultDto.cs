namespace informE.Contracts.Dtos;

public record CommandResultDto(
    Guid LogId,
    bool Succeeded,
    string Output,
    DateTimeOffset ExecutedAt
);
