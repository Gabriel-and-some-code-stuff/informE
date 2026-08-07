using informE.Domain.Entities;

namespace informE.Domain.Tests;

// Placeholder — mesmo padrão do UserTests, entidade diferente.
public class SessionTests
{
    [Fact]
    public void Construtor_DeveIniciarComoAtiva()
    {

        var session = new Session(
            ipAddress: "192.168.0.10",
            expiresAt: DateTimeOffset.Now.AddDays(7),
            lastSeenAt: DateTimeOffset.Now,
            refreshTokenHash: "hash-fake",
            userId: Guid.NewGuid());
        // Assert

        Assert.True(session.IsActive);
    }
    [Fact]
    public void Construtor_DeveDefinirLoginAt()
    {
        var dateBefore = DateTimeOffset.Now;

        var session = new Session(
            ipAddress: "192.168.0.10",
            expiresAt: DateTimeOffset.Now.AddDays(7),
            lastSeenAt: DateTimeOffset.Now,
            refreshTokenHash: "hash-fake",
            userId: Guid.NewGuid()
        );

        var dateAfter = DateTimeOffset.Now;

        Assert.InRange(session.LoginAt, dateBefore, dateAfter);
    }

    [Fact]
    public void Construtor_DeveDefinirDadosInformados()
    {
        var userId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.Now.AddDays(7);
        var lastSeenAt = DateTimeOffset.Now;

        var session = new Session(
            "192.168.0.10",
            expiresAt,
            lastSeenAt,
            "hash-fake",
            userId
        );

        Assert.Equal("192.168.0.10", session.IpAddress);
        Assert.Equal(expiresAt, session.ExpiresAt);
        Assert.Equal(lastSeenAt, session.LastSeenAt);
        Assert.Equal("hash-fake", session.RefreshTokenHash);
        Assert.Equal(userId, session.UserId);
    }

    // TODO: quando Session.Revoke() existir, testar que IsActive vira false.
    // TODO: quando Session.IsExpired() existir, testar com ExpiresAt no passado.
}
