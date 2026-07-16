using informE.Contracts.Dtos;

namespace informE.Contracts.Hubs;

// Métodos que o Server invoca no Agent (via AgentHub).
public interface IAgentClient
{
    Task RunCommand(CommandDto command);
    Task RotateKey(string newKey); // novo segredo — agente persiste com DPAPI
}
