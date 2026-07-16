namespace informE.Contracts.Dtos;

public record CommandDto(
    Guid TaskId,
    Guid LogId,      // ID do TaskExecutionLog a atualizar com o resultado
    string Script,
    string Kind      // "PowerShell" | "Batch"
);
