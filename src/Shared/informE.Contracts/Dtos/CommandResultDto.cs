namespace informE.Contracts.Dtos;

public record CommandResultDto(
    Guid TaskId, // permite checar se a MachineTask inteira terminou sem round-trip extra
    Guid LogId,
    bool Succeeded,
    string Output,
    DateTimeOffset ExecutedAt
);
