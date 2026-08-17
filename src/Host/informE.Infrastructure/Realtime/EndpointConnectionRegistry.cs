using System.Collections.Concurrent;
using informE.Application.Interfaces;

namespace informE.Infrastructure.Realtime;

// RF08. Registrado como Singleton no DI -- estado em memoria compartilhado
// entre todas as conexoes do AgentHub, vive enquanto o processo do Server vive.
public class EndpointConnectionRegistry : IEndpointConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, string> _connections = new();

    public void Register(Guid deviceId, string connectionId)
    {
        _connections[deviceId] = connectionId;
    }

    public void Remove(Guid deviceId, string connectionId)
    {
        // Remove so se a connectionId bater -- evita que um OnDisconnectedAsync
        // atrasado de uma conexao antiga apague a conexao nova apos uma reconexao rapida.
        _connections.TryRemove(new KeyValuePair<Guid, string>(deviceId, connectionId));
    }

    public bool IsOnline(Guid deviceId) => _connections.ContainsKey(deviceId);

    public string? GetConnectionId(Guid deviceId) =>
        _connections.TryGetValue(deviceId, out var connectionId) ? connectionId : null;

    public IReadOnlyCollection<Guid> OnlineDeviceIds => _connections.Keys.ToList();
}
