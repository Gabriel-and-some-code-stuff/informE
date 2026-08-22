namespace informE.Application.Models;

// AgentKey volta em TEXTO CLARO e só nesta resposta — o servidor guarda apenas o
// hash Argon2id. O agente persiste com DPAPI (ARCHITECTURE.md §4). Se perder,
// não há recuperação: precisa de um enrollment novo.
public record EnrollDeviceResponse(Guid DeviceId, string AgentKey);
