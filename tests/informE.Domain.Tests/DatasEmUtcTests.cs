using informE.Domain.Entities;
using informE.Domain.Enums;

namespace informE.Domain.Tests;

// Trava a regra que derrubou o primeiro INSERT real do projeto.
//
// O Npgsql RECUSA gravar DateTimeOffset com offset != 0 em coluna `timestamptz`:
//
//   Cannot write DateTimeOffset with Offset=-03:00:00 to PostgreSQL type
//   'timestamp with time zone', only offset 0 (UTC) is supported.
//
// Como todas as entidades usavam DateTimeOffset.Now (local, -03:00 no Brasil),
// NENHUMA linha com data jamais conseguiria ser gravada. Ficou invisível por
// meses: teste unitário não toca Postgres, e o Server não tinha endpoint que
// escrevesse. O seed foi a primeira escrita de verdade — e falhou em tudo.
//
// Estes testes rodam em milissegundos e quebram na hora se alguém reintroduzir
// `.Now` num construtor.
public class DatasEmUtcTests
{
    [Fact]
    public void User_CreatedAt_deve_ser_UTC()
    {
        var user = new User("gabriel", "g@cps.sp.gov.br", "hash", UserRole.Admin);

        Assert.Equal(TimeSpan.Zero, user.CreatedAt.Offset);
    }

    [Fact]
    public void Session_LoginAt_e_LastSeenAt_devem_ser_UTC()
    {
        var session = new Session("192.168.0.10", DateTimeOffset.UtcNow.AddDays(7), "hash", Guid.NewGuid());

        Assert.Equal(TimeSpan.Zero, session.LoginAt.Offset);
        Assert.Equal(TimeSpan.Zero, session.LastSeenAt.Offset);
    }

    [Fact]
    public void Device_RegisteredAt_e_KeyRotatedAt_devem_ser_UTC()
    {
        var device = NovoDevice();

        Assert.Equal(TimeSpan.Zero, device.RegisteredAt.Offset);
        Assert.Equal(TimeSpan.Zero, device.KeyRotatedAt.Offset);
    }

    [Fact]
    public void Alert_OccurredAt_deve_ser_UTC()
    {
        var alert = new Alert(Guid.NewGuid(), AlertType.HighCpu, "CPU alta");

        Assert.Equal(TimeSpan.Zero, alert.OccurredAt.Offset);
    }

    [Fact]
    public void AuditLog_CreatedAt_deve_ser_UTC()
    {
        var log = new AuditLog("login_ok", "192.168.0.10", Guid.NewGuid());

        Assert.Equal(TimeSpan.Zero, log.CreatedAt.Offset);
    }

    [Fact]
    public void Group_CreatedAt_deve_ser_UTC()
    {
        var group = new Group("Lab 1", "Laboratório", Guid.NewGuid());

        Assert.Equal(TimeSpan.Zero, group.CreatedAt.Offset);
    }

    [Fact]
    public void EnrollmentToken_ExpiresAt_deve_ser_UTC()
    {
        var token = new EnrollmentToken("token-abc", Guid.NewGuid());

        Assert.Equal(TimeSpan.Zero, token.ExpiresAt.Offset);
    }

    [Fact]
    public void PasswordResetToken_datas_devem_ser_UTC()
    {
        var token = new PasswordResetToken(Guid.NewGuid(), "hash");

        Assert.Equal(TimeSpan.Zero, token.CreatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, token.ExpiresAt.Offset);
    }

    [Fact]
    public void Software_DetectedAt_deve_ser_UTC()
    {
        var software = new Software("Google Chrome", "120.0");

        Assert.Equal(TimeSpan.Zero, software.DetectedAt.Offset);
    }

    // MarkSeen recebe a data de fora (vem do TelemetryDto). O contrato é que quem
    // chama passe UTC — este teste documenta isso.
    [Fact]
    public void Device_MarkSeen_preserva_o_offset_recebido()
    {
        var device = NovoDevice();

        device.MarkSeen(DateTimeOffset.UtcNow, HealthStatus.Saudavel);

        Assert.Equal(TimeSpan.Zero, device.LastSeenAt!.Value.Offset);
    }

    private static Device NovoDevice() =>
        new("PC-101", "192.168.1.10", "AA:BB:CC:01:00:01", "Windows 11", "aluno", "hash", null, null);
}
