using informE.Domain.Entities;

namespace informE.Domain.Tests;

public class SessionTests
{
    [Fact]
    public void Construtor_DeveIniciarComoAtiva()
    {
        Assert.True(NovaSessao().IsActive);
    }

    [Fact]
    public void Construtor_DeveDefinirLoginAt()
    {
        var antes = DateTimeOffset.Now;
        var session = NovaSessao();
        var depois = DateTimeOffset.Now;

        Assert.InRange(session.LoginAt, antes, depois);
    }

    [Fact]
    public void Construtor_DeveUsarOExpiresAtInformado()
    {
        // Antes o construtor fixava Now.AddHours(6), o que fazia a sessão morrer
        // antes do refresh token de 7 dias. Agora quem cria decide.
        var expiraEm = DateTimeOffset.Now.AddDays(7);

        var session = new Session("192.168.0.10", expiraEm, "hash-fake", Guid.NewGuid());

        Assert.Equal(expiraEm, session.ExpiresAt);
    }

    [Fact]
    public void Construtor_LastSeenAtDeveNascerIgualAoLoginAt()
    {
        var session = NovaSessao();

        Assert.Equal(session.LoginAt, session.LastSeenAt);
    }

    [Fact]
    public void Construtor_DeveDefinirDadosInformados()
    {
        var userId = Guid.NewGuid();

        var session = new Session("192.168.0.10", DateTimeOffset.Now.AddDays(7), "hash-fake", userId, "Chrome — Windows 11");

        Assert.Equal("192.168.0.10", session.IpAddress);
        Assert.Equal("hash-fake", session.RefreshTokenHash);
        Assert.Equal(userId, session.UserId);
        Assert.Equal("Chrome — Windows 11", session.DeviceLabel);
    }

    [Fact]
    public void Revoke_DeveDesativarASessao()
    {
        var session = NovaSessao();

        session.Revoke();

        Assert.False(session.IsActive);
    }

    [Fact]
    public void IsExpired_ComExpiresAtNoPassadoDeveSerTrue()
    {
        var session = new Session("192.168.0.10", DateTimeOffset.Now.AddMinutes(-1), "hash-fake", Guid.NewGuid());

        Assert.True(session.IsExpired());
    }

    [Fact]
    public void IsExpired_ComExpiresAtNoFuturoDeveSerFalse()
    {
        Assert.False(NovaSessao().IsExpired());
    }

    [Fact]
    public void Touch_DeveAtualizarLastSeenAt()
    {
        var session = NovaSessao();
        var depois = DateTimeOffset.Now.AddMinutes(5);

        session.Touch(depois);

        Assert.Equal(depois, session.LastSeenAt);
    }

    private static Session NovaSessao() =>
        new("192.168.0.10", DateTimeOffset.Now.AddDays(7), "hash-fake", Guid.NewGuid());
}
