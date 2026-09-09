using informE.Domain.Entities;
using informE.Domain.Enums;

namespace informE.Domain.Tests;

public class DeviceTests
{
    // Limiares calibrados pelos dados da tela de Equipamentos.
    [Theory]
    [InlineData(10f, 37f, 31f, HealthStatus.Saudavel)]  // PC-01
    [InlineData(19f, 87f, 82f, HealthStatus.Aviso)]     // PC-05: RAM 7/8, disco 210/256
    [InlineData(19f, 87f, 90f, HealthStatus.Critico)]   // PC-03: disco 230/256
    [InlineData(99f, 10f, 10f, HealthStatus.Critico)]   // CPU sozinha estoura
    [InlineData(79.9f, 79.9f, 79.9f, HealthStatus.Saudavel)] // borda de baixo
    [InlineData(80f, 0f, 0f, HealthStatus.Aviso)]       // borda exata do Aviso
    [InlineData(90f, 0f, 0f, HealthStatus.Critico)]     // borda exata do Crítico
    public void EvaluateHealth_DeveClassificarPeloPiorRecurso(float cpu, float ram, float disk, HealthStatus esperado)
    {
        Assert.Equal(esperado, Device.EvaluateHealth(cpu, ram, disk));
    }

    [Fact]
    public void MarkOffline_DeveZerarSaudeParaErro()
    {
        var device = NovoDevice();
        device.MarkSeen(DateTimeOffset.Now, HealthStatus.Saudavel);

        device.MarkOffline();

        Assert.Equal(EndpointStatus.Offline, device.Status);
        Assert.Equal(HealthStatus.Erro, device.Health);
    }

    [Fact]
    public void MarkSeen_DeveDeixarConexaoESaudeIndependentes()
    {
        var device = NovoDevice();

        device.MarkSeen(DateTimeOffset.Now, HealthStatus.Critico);

        // O caso do PC-03 na tela: conectado, mas com recurso em estado crítico.
        Assert.Equal(EndpointStatus.Online, device.Status);
        Assert.Equal(HealthStatus.Critico, device.Health);
    }

    [Fact]
    public void Device_DeveNascerComoAluno()
    {
        Assert.Equal(DeviceRole.Aluno, NovoDevice().Role);
    }

    [Fact]
    public void MarkSeen_DeveGuardarOUptimeDoSnapshot()
    {
        var device = NovoDevice();

        device.MarkSeen(DateTimeOffset.Now, HealthStatus.Saudavel, uptimeSeconds: 302_400); // 3d 12h

        Assert.Equal(302_400, device.UptimeSeconds);
    }

    [Fact]
    public void MarkSeen_SemUptimeNaoDeveApagarOValorAnterior()
    {
        // O OnConnectedAsync do AgentHub marca Online antes de existir snapshot;
        // não pode zerar o uptime que já estava lá.
        var device = NovoDevice();
        device.MarkSeen(DateTimeOffset.Now, HealthStatus.Saudavel, uptimeSeconds: 1000);

        device.MarkSeen(DateTimeOffset.Now, HealthStatus.Saudavel);

        Assert.Equal(1000, device.UptimeSeconds);
    }

    [Fact]
    public void MarkOffline_DeveLimparOUptime()
    {
        // Máquina desligada não tem uptime — a tela mostra "—" nessas linhas.
        var device = NovoDevice();
        device.MarkSeen(DateTimeOffset.Now, HealthStatus.Saudavel, uptimeSeconds: 1000);

        device.MarkOffline();

        Assert.Null(device.UptimeSeconds);
    }

    private static Device NovoDevice() =>
        new("PC-01", "192.168.1.10", "AA:BB:CC:DD:EE:FF", "Windows 11", "aluno", "hash-fake", null, null);

    // ── Percentuais correntes ────────────────────────────────────────────────
    // Antes de existirem estes campos, EvaluateHealth reduzia CPU/RAM/disco a um
    // enum e os numeros eram descartados — a tela de detalhe nao tinha o dado.

    [Fact]
    public void MarkSeen_DeveGuardarOsPercentuaisDoSnapshot()
    {
        var device = NovoDevice();

        device.MarkSeen(DateTimeOffset.UtcNow, HealthStatus.Critico,
            uptimeSeconds: 1000, cpuPercent: 35.9f, ramPercent: 90.5f, diskPercent: 89.1f);

        Assert.Equal(35.9f, device.CpuPercent);
        Assert.Equal(90.5f, device.RamPercent);
        Assert.Equal(89.1f, device.DiskPercent);
    }

    [Fact]
    public void MarkSeen_SemPercentuaisNaoDeveApagarOsAnteriores()
    {
        // AgentHub.OnConnectedAsync chama MarkSeen(now, device.Health) sem
        // telemetria. Se isso zerasse os percentuais, reconectar apagaria a
        // ultima leitura conhecida da tela.
        var device = NovoDevice();
        device.MarkSeen(DateTimeOffset.UtcNow, HealthStatus.Aviso,
            cpuPercent: 12f, ramPercent: 34f, diskPercent: 56f);

        device.MarkSeen(DateTimeOffset.UtcNow, device.Health);

        Assert.Equal(12f, device.CpuPercent);
        Assert.Equal(34f, device.RamPercent);
        Assert.Equal(56f, device.DiskPercent);
    }

    [Fact]
    public void MarkOffline_DeveLimparOsPercentuais()
    {
        // Percentual de maquina offline e leitura velha apresentada como atual.
        // A tela mostra "—", nunca o ultimo valor nem 0%.
        var device = NovoDevice();
        device.MarkSeen(DateTimeOffset.UtcNow, HealthStatus.Saudavel,
            cpuPercent: 40f, ramPercent: 50f, diskPercent: 60f);

        device.MarkOffline();

        Assert.Null(device.CpuPercent);
        Assert.Null(device.RamPercent);
        Assert.Null(device.DiskPercent);
    }

    [Fact]
    public void Percentuais_DevemNascerNulosAntesDoPrimeiroSnapshot()
    {
        var device = NovoDevice();

        Assert.Null(device.CpuPercent);
        Assert.Null(device.RamPercent);
        Assert.Null(device.DiskPercent);
    }
}
