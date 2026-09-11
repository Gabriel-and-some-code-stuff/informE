using informE.Application.Interfaces;
using informE.Domain.Entities;
using informE.Domain.Enums;
using informE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace informE.Infrastructure.BackgroundJobs;

// RN03: marca como Offline quem parou de mandar heartbeat.
//
// POR QUE É UM BackgroundService E NÃO UM USE CASE: ficar offline é a AUSÊNCIA de
// um evento, não a reação a um. Nenhuma requisição chega dizendo "parei de
// responder" — alguém precisa varrer o relógio. Mesmo raciocínio do
// ARCHITECTURE.md §4 para sessões ociosas.
//
// Também é o produtor que faltava para AlertType.DeviceOffline: até aqui nada no
// sistema criava Alert, e a faixa "Offline" do gráfico ficava sempre vazia.
public class DeviceOfflineSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<MonitoringOptions> options,
    ILogger<DeviceOfflineSweeper> logger) : BackgroundService
{
    private readonly MonitoringOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(_options.SweepIntervalMinutes);

        logger.LogInformation(
            "Varredura de offline ativa: a cada {Intervalo} min, marcando offline quem não reporta há {Limiar} min.",
            _options.SweepIntervalMinutes, _options.OfflineThresholdMinutes);

        using var timer = new PeriodicTimer(intervalo);

        try
        {
            // do/while: varre uma vez no boot e depois a cada tick. Sem isso, um
            // restart do servidor deixaria máquinas caídas aparecendo online pelo
            // primeiro intervalo inteiro.
            do
            {
                try
                {
                    await VarrerAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Uma falha (banco fora do ar, por exemplo) não pode matar o job:
                    // sem este catch, o serviço morre calado e ninguém mais fica offline.
                    logger.LogError(ex, "Falha na varredura de offline. Tentando de novo no próximo ciclo.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Varredura de offline encerrada.");
        }
    }

    private async Task VarrerAsync(CancellationToken ct)
    {
        // BackgroundService é singleton; DbContext é scoped. Sem o escopo próprio,
        // o DbContext viveria pra sempre e acumularia entidades rastreadas.
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<IDashboardNotifier>();

        var limite = DateTimeOffset.UtcNow.AddMinutes(-_options.OfflineThresholdMinutes);

        // Só quem ESTÁ Online e sumiu. Unknown (enrollado, nunca reportou) fica de
        // fora de propósito: nunca esteve online, então não "caiu".
        var sumidos = await db.Devices
            .Where(d => d.Status == EndpointStatus.Online
                     && (d.LastSeenAt == null || d.LastSeenAt < limite))
            .ToListAsync(ct);

        if (sumidos.Count == 0)
            return;

        foreach (var device in sumidos)
        {
            device.MarkOffline();

            db.Alerts.Add(new Alert(
                device.Id,
                AlertType.DeviceOffline,
                $"{device.Hostname}: sem sinal do agente há mais de {_options.OfflineThresholdMinutes} minutos."));
        }

        await db.SaveChangesAsync(ct);

        // Notifica só depois de persistir: se o SaveChanges falhar, o dashboard
        // não mostra um offline que não aconteceu.
        foreach (var device in sumidos)
            await notifier.DeviceStatusChangedAsync(device.Id, device.Status, device.Health, ct);

        logger.LogInformation("{Quantidade} máquina(s) marcada(s) como offline.", sumidos.Count);
    }
}
