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

    private static Device NovoDevice() =>
        new("PC-01", "192.168.1.10", "AA:BB:CC:DD:EE:FF", "Windows 11", "aluno", "hash-fake", null, null);
}
