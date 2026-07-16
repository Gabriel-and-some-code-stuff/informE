namespace informE.Application.Abstractions;

// RF08: garante 1 conexão SignalR por deviceId. Implementado em memória (Infrastructure).
public interface IEndpointConnectionRegistry
{
    void Register(Guid deviceId, string connectionId);
    void Remove(Guid deviceId, string connectionId);
    bool IsOnline(Guid deviceId);
    string? GetConnectionId(Guid deviceId);
    IReadOnlyCollection<Guid> OnlineDeviceIds { get; }
}
