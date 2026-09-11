using System.Diagnostics;
using System.Management;

namespace informE.Agent.Worker;

public record SystemSnapshot(float CpuPercent, float RamPercent, float DiskPercent, int UptimeSeconds);

// Lê o estado atual da máquina. RF02: CPU, RAM e disco — sem histórico.
//
// Windows-only na prática (WMI + PerformanceCounter), mas com guardas de
// OperatingSystem.IsWindows() porque o CI builda esta solução no ubuntu. Fora do
// Windows devolve zeros em vez de estourar.
public class SystemSnapshotCollector : IDisposable
{
    private readonly ILogger<SystemSnapshotCollector> _logger;
    private readonly PerformanceCounter? _cpu;

    public SystemSnapshotCollector(ILogger<SystemSnapshotCollector> logger)
    {
        _logger = logger;

        if (!OperatingSystem.IsWindows())
        {
            logger.LogWarning("Coleta de métricas só funciona no Windows. Snapshots virão zerados.");
            return;
        }

        try
        {
            _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");

            // A PRIMEIRA leitura de um PerformanceCounter sempre volta 0: ele
            // precisa de duas amostras para calcular a taxa. Descartada aqui para
            // o primeiro snapshot real já vir com valor.
            _cpu.NextValue();
        }
        catch (Exception ex)
        {
            // Contadores de performance podem estar corrompidos no Windows
            // (acontece). Melhor reportar CPU zerada do que o agente não subir.
            logger.LogError(ex, "Não foi possível abrir o contador de CPU.");
            _cpu = null;
        }
    }

    public SystemSnapshot Coletar()
    {
        // TickCount64 = ms desde o boot. Não precisa de WMI e não sofre com
        // relógio ajustado, diferente de calcular a partir de LastBootUpTime.
        var uptime = (int)(Environment.TickCount64 / 1000);

        if (!OperatingSystem.IsWindows())
            return new SystemSnapshot(0, 0, 0, uptime);

        return new SystemSnapshot(
            CpuPercent: LerCpu(),
            RamPercent: LerRam(),
            DiskPercent: LerDisco(),
            UptimeSeconds: uptime);
    }

    private float LerCpu()
    {
        // O IsWindows() aqui é redundante em runtime (_cpu só existe no Windows),
        // mas o analisador CA1416 não infere isso a partir do campo — e com
        // TreatWarningsAsErrors o build quebra sem ele.
        if (_cpu is null || !OperatingSystem.IsWindows())
            return 0;

        try
        {
            return Math.Clamp(_cpu.NextValue(), 0, 100);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler CPU.");
            return 0;
        }
    }

    // WMI é a fonte de verdade para memória FÍSICA. A alternativa do GC
    // (GetGCMemoryInfo) reporta a visão do runtime, não a da máquina.
    private float LerRam()
    {
        if (!OperatingSystem.IsWindows())
            return 0;

        try
        {
            using var busca = new ManagementObjectSearcher(
                "SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");

            foreach (var item in busca.Get().Cast<ManagementObject>())
            {
                var livre = Convert.ToDouble(item["FreePhysicalMemory"]);
                var total = Convert.ToDouble(item["TotalVisibleMemorySize"]);

                if (total <= 0)
                    return 0;

                return (float)Math.Round((total - livre) / total * 100, 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler memória via WMI.");
        }

        return 0;
    }

    // Só o disco do sistema. Monitorar todos os volumes exigiria mudar o
    // TelemetryDto para uma lista — fora do escopo do MVP.
    private float LerDisco()
    {
        try
        {
            var sistema = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(sistema);

            if (!drive.IsReady || drive.TotalSize <= 0)
                return 0;

            var usado = drive.TotalSize - drive.AvailableFreeSpace;
            return (float)Math.Round((double)usado / drive.TotalSize * 100, 1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler disco.");
            return 0;
        }
    }

    public void Dispose() => _cpu?.Dispose();
}
