using informE.Domain.Entities;

namespace informE.Domain.Tests;

// Placeholder — mesmo padrão do UserTests, entidade diferente.
public class SessionTests
{
    [Fact]
    public void Construtor_DeveIniciarComoAtiva()
    {
        var session = new Session("192.168.0.10", DateTimeOffset.Now, "hash-fake", Guid.NewGuid());

        Assert.True(session.IsActive);
    }

    [Fact]
    public void Construtor_DeveDefinirLoginAt()
    {
        var antes = DateTimeOffset.Now;
        var session = new Session("192.168.0.10", DateTimeOffset.Now, "hash-fake", Guid.NewGuid());
        var depois = DateTimeOffset.Now;

        Assert.InRange(session.LoginAt, antes, depois);
    }

    [Fact]
    public void Construtor_ExpiresAtDeveSerMaiorQueLoginAt()
    {
        var session = new Session("192.168.0.10", DateTimeOffset.Now, "hash-fake", Guid.NewGuid());

        Assert.True(session.ExpiresAt > session.LoginAt);
    }

    [Fact]
    public void Construtor_DeveDefinirDadosInformados()
    {
        var userId = Guid.NewGuid();
        var lastSeenAt = DateTimeOffset.Now;

        var session = new Session("192.168.0.10", lastSeenAt, "hash-fake", userId);

        Assert.Equal("192.168.0.10", session.IpAddress);
        Assert.Equal(lastSeenAt, session.LastSeenAt);
        Assert.Equal("hash-fake", session.RefreshTokenHash);
        Assert.Equal(userId, session.UserId);
    }

    // TODO: quando Session.Revoke() existir, testar que IsActive vira false.
    // TODO: quando Session.IsExpired() existir, testar com ExpiresAt no passado.
}
