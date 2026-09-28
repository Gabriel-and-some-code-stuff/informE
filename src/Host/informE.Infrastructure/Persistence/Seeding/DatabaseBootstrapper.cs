using informE.Application.Interfaces;
using informE.Domain.Enums;
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

    // Nenhum agente pode estar conectado a um processo que acabou de subir: o
    // EndpointConnectionRegistry vive em memória e nasce vazio.
    //
    // Sem isto, `devices.status` continuava dizendo Online depois de todo
    // restart — e só voltava à verdade quando o DeviceOfflineSweeper passasse,
    // até 90 minutos depois. Nesse meio tempo a tela mostrava máquinas online
    // que não existiam, e disparar nelas devolvia "offline no momento do
    // disparo" para todas.
    //
    // Os agentes reconectam sozinhos em segundos (RF06) e voltam a Online.
    public async Task<int> ResetarConexoesAsync(CancellationToken ct = default)
    {
        var afetados = await db.Devices
            .Where(d => d.Status == EndpointStatus.Online)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, EndpointStatus.Offline)
                .SetProperty(d => d.Health, HealthStatus.Erro)
                .SetProperty(d => d.UptimeSeconds, (int?)null), ct);

        if (afetados > 0)
            logger.LogInformation(
                "{Quantidade} máquina(s) marcada(s) como offline no boot — nenhum agente está " +
                "conectado ainda. Elas voltam a Online conforme reconectarem.", afetados);

        return afetados;
    }

    // Idempotente, e em DOIS conjuntos independentes: contas e parque.
    //
    // Antes o guard era um `if (db.Users.Any()) return;` só. Quem criasse uma
    // conta pelo Scalar antes do primeiro seed ficava sem as 105 máquinas para
    // sempre, e o log dizia apenas "Banco já tem dados — seed ignorado". Como o
    // parque é justamente o que se precisa para testar execução remota, o
    // sintoma era um banco que parecia semeado e não era.
    // Renova o parque de DEMONSTRACAO a cada boot.
    //
    // O BUG QUE ISTO CONSERTA: o seed grava `last_seen_at` como "agora" e roda
    // uma vez so. O DeviceOfflineSweeper marca offline quem nao fala ha 90 min.
    // Resultado: 90 minutos depois de criar o banco, as 105 maquinas de
    // demonstracao viravam offline PARA SEMPRE.
    //
    // Aconteceu numa apresentacao: o banco tinha sido criado no dia anterior, e
    // a tela abriu com "Offline: 105". Pior tipo de defeito -- nao quebra nada,
    // so faz o produto parecer morto.
    //
    // So mexe em maquina de demonstracao (SeedData.EhDeDemonstracao, pelo nome
    // que o proprio seed gera). Maquina com agente de verdade nao e tocada: se
    // fosse, uma maquina desligada apareceria eternamente online, que e mentira
    // pior que parque morto.
    public async Task<int> RenovarParqueDeDemonstracaoAsync(CancellationToken ct = default)
    {
        var demo = await db.Devices
            .Where(d => d.Hostname.StartsWith("PC-") || d.Hostname.StartsWith("PROF-LAB"))
            .OrderBy(d => d.Hostname)
            .ToListAsync(ct);

        if (demo.Count == 0)
            return 0;

        var agora = DateTimeOffset.UtcNow;

        // SEM guarda de "o dado ainda esta fresco".
        //
        // Tentei essa guarda e ela nao serve: o ResetarConexoesAsync, que roda
        // logo antes, marca tudo offline SEM mexer no last_seen_at. O dado fica
        // parecendo fresco e a renovacao seria ignorada -- deixando o parque
        // morto exatamente no caso mais comum, que e reiniciar o Server.
        //
        // Renovar sempre custa um UPDATE de 105 linhas por boot. E de graca, e o
        // parque de demonstracao deve estar vivo sempre que o app esta no ar.

        for (var i = 0; i < demo.Count; i++)
            SeedData.AplicarEstado(demo[i], i, agora);

        await db.SaveChangesAsync(ct);

        var online = demo.Count(d => d.Status == EndpointStatus.Online);

        logger.LogInformation(
            "Parque de demonstração renovado: {Online} online, {Offline} offline de {Total}.",
            online, demo.Count - online, demo.Count);

        return demo.Count;
    }

    public async Task<bool> SeedDevelopmentDataAsync(CancellationToken ct = default)
    {
        var usuariosExistentes = await db.Users
            .Select(u => new { u.Id, u.Role })
            .ToListAsync(ct);

        var semearContas = usuariosExistentes.Count == 0;
        var semearParque = !await db.Devices.AnyAsync(ct);

        if (!semearContas && !semearParque)
        {
            logger.LogInformation("Banco já tem contas e parque — seed ignorado.");
            return false;
        }

        logger.LogWarning(
            "Semeando dados de DESENVOLVIMENTO. Todos os usuários usam a senha '{Senha}'. " +
            "Isto só roda em Development — ver DatabaseBootstrapper.", SeedData.SenhaPadrao);

        // Um hash de senha e um de chave de agente, reaproveitados. Argon2id usa
        // 64 MB e ~100 ms por chamada: hashear 105 devices individualmente
        // colocaria mais de 10 segundos no boot sem ganho nenhum para dev.
        var senhaHash = passwordHasher.Hash(SeedData.SenhaPadrao);
        var agentKeyHash = passwordHasher.Hash("chave-de-agente-dev");

        // Grupos e tarefas precisam de um dono que exista. Se as contas do seed
        // não vão entrar, o dono passa a ser o SuperAdmin já cadastrado.
        Guid? donoExistente = semearContas
            ? null
            : (usuariosExistentes.FirstOrDefault(u => u.Role == UserRole.SuperAdmin)
               ?? usuariosExistentes[0]).Id;

        var massa = SeedData.Montar(senhaHash, agentKeyHash, donoExistente);

        // Ordem importa: FK. Usuário antes de grupo (OwnerId), grupo antes de
        // device (GroupId), device antes de alerta/log/métrica.
        if (semearContas)
            await db.Users.AddRangeAsync(massa.Usuarios, ct);

        if (semearParque)
        {
            await db.Groups.AddRangeAsync(massa.Grupos, ct);
            await db.Devices.AddRangeAsync(massa.Devices, ct);
            await db.MachineTasks.AddRangeAsync(massa.Tarefas, ct);
            await db.TaskExecutionLogs.AddRangeAsync(massa.Logs, ct);
            await db.Alerts.AddRangeAsync(massa.Alertas, ct);
            await db.DeviceDailyMetrics.AddRangeAsync(massa.Metricas, ct);
            await db.DeviceInfos.AddRangeAsync(massa.Hardware, ct);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seed concluído: {Usuarios} usuários, {Grupos} grupos, {Devices} máquinas, " +
            "{Alertas} alertas, {Tarefas} execuções, {Metricas} métricas diárias.",
            semearContas ? massa.Usuarios.Count : 0,
            semearParque ? massa.Grupos.Count : 0,
            semearParque ? massa.Devices.Count : 0,
            semearParque ? massa.Alertas.Count : 0,
            semearParque ? massa.Tarefas.Count : 0,
            semearParque ? massa.Metricas.Count : 0);

        return true;
    }
}
