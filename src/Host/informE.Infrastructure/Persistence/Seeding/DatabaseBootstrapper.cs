using informE.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace informE.Infrastructure.Persistence.Seeding;

// Deixa o banco utilizável a partir do zero, em um comando.
//
// POR QUE ISSO EXISTE: o volume do Postgres é um volume nomeado do Docker
// (`informe_pgdata`), que vive no WSL2 e NÃO está no repositório — nem deveria,
// dado que dado de banco não se versiona. Consequência: quem clona o repo, ou
// apaga o volume, começa com o banco VAZIO. Antes disso aqui, a pessoa tinha que
// saber rodar `dotnet ef database update` na mão, e o `start-db.ps1` (o script do
// dia a dia) nem fazia isso — depois de um `git pull` com migration nova, o banco
// ficava defasado silenciosamente.
//
// Agora `dotnet run` no Server basta: migra e semeia sozinho.
public class DatabaseBootstrapper(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    ILogger<DatabaseBootstrapper> logger)
{
    public async Task MigrateAsync(CancellationToken ct = default)
    {
        var pendentes = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();

        if (pendentes.Count == 0)
        {
            logger.LogInformation("Banco já está na última migration.");
            return;
        }

        logger.LogInformation("Aplicando {Quantidade} migration(s): {Migrations}",
            pendentes.Count, string.Join(", ", pendentes));

        await db.Database.MigrateAsync(ct);

        logger.LogInformation("Migrations aplicadas.");
    }

    // Idempotente: se já existe usuário, não faz nada. Rodar duas vezes não
    // duplica massa nem estoura índice único.
    public async Task SeedDevelopmentDataAsync(CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            logger.LogInformation("Banco já tem dados — seed ignorado.");
            return;
        }

        logger.LogWarning(
            "Semeando dados de DESENVOLVIMENTO. Todos os usuários usam a senha '{Senha}'. " +
            "Isto só roda em Development — ver DatabaseBootstrapper.", SeedData.SenhaPadrao);

        // Um hash de senha e um de chave de agente, reaproveitados. Argon2id usa
        // 64 MB e ~100 ms por chamada: hashear 105 devices individualmente
        // colocaria mais de 10 segundos no boot sem ganho nenhum para dev.
        var senhaHash = passwordHasher.Hash(SeedData.SenhaPadrao);
        var agentKeyHash = passwordHasher.Hash("chave-de-agente-dev");

        var massa = SeedData.Montar(senhaHash, agentKeyHash);

        // Ordem importa: FK. Usuário antes de grupo (OwnerId), grupo antes de
        // device (GroupId), device antes de alerta/log/métrica.
        await db.Users.AddRangeAsync(massa.Usuarios, ct);
        await db.Groups.AddRangeAsync(massa.Grupos, ct);
        await db.Devices.AddRangeAsync(massa.Devices, ct);
        await db.MachineTasks.AddRangeAsync(massa.Tarefas, ct);
        await db.TaskExecutionLogs.AddRangeAsync(massa.Logs, ct);
        await db.Alerts.AddRangeAsync(massa.Alertas, ct);
        await db.DeviceDailyMetrics.AddRangeAsync(massa.Metricas, ct);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seed concluído: {Usuarios} usuários, {Grupos} grupos, {Devices} máquinas, " +
            "{Alertas} alertas, {Tarefas} execuções, {Metricas} métricas diárias.",
            massa.Usuarios.Count, massa.Grupos.Count, massa.Devices.Count,
            massa.Alertas.Count, massa.Tarefas.Count, massa.Metricas.Count);
    }
}
