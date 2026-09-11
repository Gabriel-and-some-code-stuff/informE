using System.Security.Cryptography;
using informE.Application.Interfaces;
using informE.Contracts.Hubs;
using informE.Infrastructure.Persistence;
using informE.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace informE.Infrastructure.BackgroundJobs;

// RF13: rotação proativa da chave por máquina.
//
// É um jOB porque quem decide rotacionar é a AUSÊNCIA de evento — a idade da chave,
// não uma requisição. Mesmo raciocínio dos outros dois sweepers.
//
// Os dois lados da estória já existiam: o repositório persiste hash novo
// (`DeviceRepository.RotateKeyAsync`) e o agente aceita a chave nova
// (`IAgentClient.RotateKey` → `AgentWorker` grava com DPAPI). Faltava só o disparo.
//
// REGRA DE SEGURANÇA: só rotaciona quem está CONECTADO agora. Rotacionar um device
// offline gravaria o hash novo sem o agente receber a chave, e a máquina só voltaria
// com re-enroll — o sweeper tenta de novo no próximo ciclo quando ela subir.
public class KeyRotationSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<MonitoringOptions> options,
    ILogger<KeyRotationSweeper> logger) : BackgroundService
{
    private const int TamanhoDaChaveEmBytes = 32;
    private readonly MonitoringOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(_options.KeyRotationIntervalMinutes);

        logger.LogInformation(
            "Rotação de chave ativa: a cada {Intervalo} min, rotacionando chaves com mais de {Idade} dias.",
            _options.KeyRotationIntervalMinutes, _options.KeyMaxAgeDays);

        using var timer = new PeriodicTimer(intervalo);

        try
        {
            // do/while (igual ao DeviceOfflineSweeper): varre uma vez no boot, depois a cada tick.
            do
            {
                try
                {
                    await VarrerAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Banco fora do ar não pode matar o job: sem o catch o serviço morre calado.
                    logger.LogError(ex, "Falha na varredura de rotação de chave. Tentando no próximo ciclo.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Rotação de chave encerrada.");
        }
    }

    private async Task VarrerAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<AgentHub, IAgentClient>>();
        var registry = scope.ServiceProvider.GetRequiredService<IEndpointConnectionRegistry>();

        // KeyRotatedAt nasce preenchido (Device.cs) e é tocado a cada rotação —
        // "mais velho que o limite" pega tanto quem nunca rotacionou quanto quem já.
        var limite = DateTimeOffset.UtcNow.AddDays(-_options.KeyMaxAgeDays);

        var vencidas = await db.Devices
            .Where(d => d.KeyRotatedAt < limite)
            .ToListAsync(ct);

        if (vencidas.Count == 0)
            return;

        foreach (var device in vencidas)
        {
            var connectionId = registry.GetConnectionId(device.Id);

            if (connectionId is null)
            {
                logger.LogInformation("Device {Hostname} offline — rotação adiada para o próximo ciclo.", device.Hostname);
                continue;
            }

            // Chave nova: texto claro vai só pro agente; o banco guarda o hash (RNG igual ao enroll).
            var chaveNova = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TamanhoDaChaveEmBytes));

            // 1) Persiste o hash novo primeiro (método de domínio que também toca KeyRotatedAt).
            device.UpdateAgentHashKey(passwordHasher.Hash(chaveNova));
            await db.SaveChangesAsync(ct);

            // 2) Depois empurra o texto claro pela conexão já autenticada.
            //
            // O contrato não tem ack: se o push falhar no meio, o agente fica com a chave
            // antiga e o Host com o hash novo — lockout na próxima conexão, resolvido por
            // re-enroll. É por isso que só rotacionamos online; a janela é de milissegundos.
            // Um push quebrado não mata os demais.
            try
            {
                await hub.Clients.Client(connectionId).RotateKey(chaveNova);
                logger.LogInformation("Chave rotacionada para {Hostname}.", device.Hostname);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Chave de {Hostname} já rotacionada no banco, mas o push falhou.", device.Hostname);
            }
        }
    }
}
