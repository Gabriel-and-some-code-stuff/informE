using informE.Domain.Entities;

namespace informE.Domain.Tests;

// Placeholder — mesmo padrão do UserTests, entidade diferente.
public class SessionTests
{
    [Fact]
    public void Construtor_DeveIniciarComoAtiva()
    {
        // Arrange + Act
        var session = new Session(
            ipAddress: "192.168.0.10",
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            lastSeenAt: DateTimeOffset.UtcNow,
            refreshTokenHash: "hash-fake",
            userId: Guid.NewGuid());

        // Assert
        Assert.True(session.IsActive);
    }

    // TODO: quando Session.Revoke() existir, testar que IsActive vira false.
    // TODO: quando Session.IsExpired() existir, testar com ExpiresAt no passado.
}
