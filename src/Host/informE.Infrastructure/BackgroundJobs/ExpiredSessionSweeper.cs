using informE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace informE.Infrastructure.BackgroundJobs;

// Revoga sessões vencidas. Previsto no ARCHITECTURE.md §4 ("um BackgroundService
// varre e revoga ociosos") e nunca implementado.
//
// POR QUE IMPORTA MAIS DO QUE PARECE: o LoginUseCase conta sessões vigentes para
// aplicar o limite de 3 dispositivos do Admin. Sem esta varredura, sessão vencida
// continua com IsActive = true no banco. O use case já filtra por IsExpired() em
// memória justamente por isso — mas o filtro é remendo; o certo é o banco não
// guardar sessão morta como ativa.
public class ExpiredSessionSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<MonitoringOptions> options,
    ILogger<ExpiredSessionSweeper> logger) : BackgroundService
{
    private readonly MonitoringOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.SweepIntervalMinutes));

        logger.LogInformation("Varredura de sessões vencidas ativa: a cada {Intervalo} min.",
            _options.SweepIntervalMinutes);

        try
        {
            do
            {
                try
                {
                    await VarrerAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Falha na varredura de sessões. Tentando de novo no próximo ciclo.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Varredura de sessões encerrada.");
        }
    }

    private async Task VarrerAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agora = DateTimeOffset.UtcNow;

        // ExecuteUpdateAsync: um UPDATE só no banco, sem trazer as linhas para a
        // memória. Aqui não há regra de domínio a aplicar além de virar o flag.
        var revogadas = await db.Sessions
            .Where(s => s.IsActive && s.ExpiresAt < agora)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);

        if (revogadas > 0)
            logger.LogInformation("{Quantidade} sessão(ões) vencida(s) revogada(s).", revogadas);
    }
}
