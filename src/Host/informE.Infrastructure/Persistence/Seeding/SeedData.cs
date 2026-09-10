using informE.Domain;
using informE.Domain.Entities;
using informE.Domain.Enums;
using TaskStatus = informE.Domain.Enums.TaskStatus; // desambigua de System.Threading.Tasks.TaskStatus

namespace informE.Infrastructure.Persistence.Seeding;

// Massa de dados de desenvolvimento. Espelha os números das telas do Figma:
// 5 grupos, 105 máquinas (21 por grupo: 1 do professor + 20 de aluno), 98 online
// e 7 offline.
//
// TUDO DETERMINÍSTICO: `Random` com semente fixa. Todo mundo do time vê
// exatamente os mesmos números na tela, o que torna "na minha máquina aparece
// diferente" impossível.
//
// ⚠️ NUNCA roda fora de Development — ver DatabaseBootstrapper.
public static class SeedData
{
    private const int SementeFixa = 20260911; // data da entrega, só pra ser memorável
    private const int GruposTotal = 5;
    private const int AlunosPorGrupo = 20;
    private const int DevicesOffline = 7;

    // Senha única de todos os usuários semeados. Documentada em docs/ambiente-banco.md.
    public const string SenhaPadrao = "informe123";

    public static readonly string[] NomesDeGrupo =
        ["Lab 1", "Lab 2", "Lab 3", "Lab 4", "Biblioteca"];

    public record Semeadura(
        List<User> Usuarios,
        List<Group> Grupos,
        List<Device> Devices,
        List<Alert> Alertas,
        List<MachineTask> Tarefas,
        List<TaskExecutionLog> Logs,
        List<DeviceDailyMetrics> Metricas,
        List<DeviceInfo> Hardware);

    // senhaHash e agentKeyHash entram prontos: hashear 105 vezes com Argon2
    // (64 MB por chamada) travaria o boot. Um hash só, reaproveitado — é dev.
    // ownerIdExistente: quando o banco JÁ tem usuários (alguém criou uma conta
    // pelo Scalar antes do primeiro seed) os usuários semeados não entram, e
    // grupos/tarefas precisam apontar para um dono que exista de verdade. Sem
    // isso a FK quebraria — ou, pior, o seed inteiro seria pulado e o parque de
    // 105 máquinas nunca apareceria. Ver DatabaseBootstrapper.
    public static Semeadura Montar(string senhaHash, string agentKeyHash, Guid? ownerIdExistente = null)
    {
        var rng = new Random(SementeFixa);
        var agora = DateTimeOffset.UtcNow;

        var usuarios = MontarUsuarios(senhaHash);
        var ownerId = ownerIdExistente ?? usuarios[0].Id;

        var grupos = MontarGrupos(ownerId, usuarios);
        var devices = MontarDevices(grupos, agentKeyHash, rng, agora);
        var alertas = MontarAlertas(devices, rng, agora);
        var (tarefas, logs) = MontarTarefas(devices, ownerId, agora);
        var metricas = MontarMetricas(devices, rng, agora);
        var hardware = MontarHardware(devices, rng);

        return new Semeadura(usuarios, grupos, devices, alertas, tarefas, logs, metricas, hardware);
    }

    // Inventario de hardware das maquinas de demonstracao.
    //
    // O AGENTE NAO COLETA hardware (SystemSnapshotCollector devolve so
    // CPU/RAM/disco/uptime), entao maquina real continua vindo com o bloco
    // `hardware` nulo e a tela mostra "Nao disponivel" -- que e o comportamento
    // correto e honesto.
    //
    // Sem estas linhas, PORÉM, a tabela info_devices ficava VAZIA e o bloco era
    // nulo em TODAS as 105 maquinas: a tela parecia quebrada em vez de honesta.
    // Massa de demonstracao resolve isso sem mentir sobre nenhuma maquina real.
    private static List<DeviceInfo> MontarHardware(List<Device> devices, Random rng)
    {
        // Tres perfis de maquina, como um parque de laboratorio de verdade:
        // as antigas com DDR3 e HD, as novas com DDR5 e SSD.
        (string Cpu, string Gpu, int RamGb, RamType Ram, int StorageGb, StorageType Disco, string Placa)[] perfis =
        [
            ("Intel Core i3-7100", "Intel HD Graphics 630", 8, RamType.DDR3, 500, StorageType.HD, "Dell OptiPlex 3050"),
            ("Intel Core i5-10400", "Intel UHD Graphics 630", 16, RamType.DDR4, 256, StorageType.SSD, "Dell OptiPlex 5080"),
            ("AMD Ryzen 5 5600G", "AMD Radeon Graphics", 16, RamType.DDR5, 512, StorageType.SSD, "ASUS PRIME B550M"),
        ];

        return devices.Select(device =>
        {
            var p = perfis[rng.Next(perfis.Length)];

            // BIOS nulo em ~1 a cada 5: e exatamente o que acontece no mundo real
            // (fabricante que nao publica, ou "To be filled by O.E.M."), e mantem
            // a tela exercitando o caminho de "Nao disponivel".
            var bios = rng.Next(5) == 0 ? null : $"{1 + rng.Next(3)}.{rng.Next(10)}.{rng.Next(10)}";

            return new DeviceInfo(device.Id, p.Cpu, p.Gpu, p.RamGb, p.Ram, p.StorageGb, p.Disco, p.Placa, bios);
        }).ToList();
    }

    private static List<User> MontarUsuarios(string senhaHash) =>
    [
        Usuario("admin", "admin@cps.sp.gov.br", senhaHash, UserRole.SuperAdmin),
        Usuario("jessica", "jessica@cps.sp.gov.br", senhaHash, UserRole.Admin),
        Usuario("romeu", "romeu@cps.sp.gov.br", senhaHash, UserRole.Viewer),
        Usuario("celina", "celina@cps.sp.gov.br", senhaHash, UserRole.Viewer),
        Usuario("gislene", "gislene@cps.sp.gov.br", senhaHash, UserRole.Viewer),
        Inativo(Usuario("luci", "luci@cps.sp.gov.br", senhaHash, UserRole.Viewer)),
    ];

    private static User Usuario(string nome, string email, string hash, UserRole papel) =>
        new(nome, email, hash, papel) { Id = Guid.NewGuid() };

    // A tela de Administração de Contas mostra a Prof. Luci como "Inativo" —
    // sem um usuário inativo na massa, esse estado nunca aparece.
    private static User Inativo(User user)
    {
        user.Deactivate();
        return user;
    }

    // A POSSE do laboratorio e o que da escopo ao Viewer.
    //
    // Antes os 5 grupos eram todos do admin, e o fluxo do Viewer nao tinha como
    // ser demonstrado: ele logava e o /groups dele vinha VAZIO, porque o escopo
    // e por Group.OwnerId (ver docs/politica-login-sessao.md §1 -- escopo real
    // por N-N e Fase 2; um Viewer com UM laboratorio cabe no modelo de hoje).
    //
    // Cada Viewer ativo recebe um laboratorio, em ordem; os que sobram ficam com
    // o admin. Com 3 Viewers ativos e 5 grupos: Lab 1, 2 e 3 tem professor
    // responsavel, Lab 4 e 5 ficam so com a administracao.
    private static List<Group> MontarGrupos(Guid ownerId, List<User> usuarios)
    {
        var professores = usuarios
            .Where(u => u.Role == UserRole.Viewer && u.IsActive)
            .Select(u => u.Id)
            .ToList();

        return [.. NomesDeGrupo.Select((nome, indice) =>
            new Group(
                nome,
                $"Laboratório {nome} — Etec Albert Einstein",
                indice < professores.Count ? professores[indice] : ownerId)
            {
                Id = Guid.NewGuid()
            })];
    }

    private static List<Device> MontarDevices(List<Group> grupos, string agentKeyHash, Random rng, DateTimeOffset agora)
    {
        var devices = new List<Device>();

        for (var g = 0; g < GruposTotal; g++)
        {
            var grupo = grupos[g];
            var numeroDoGrupo = g + 1;

            // Máquina do professor. Hostname precisa passar no ValidateHostname:
            // no máximo 15 chars, só letra/número/hífen, nunca só dígitos.
            devices.Add(NovoDevice(
                hostname: $"PROF-LAB{numeroDoGrupo}",
                ip: $"192.168.{numeroDoGrupo}.10",
                mac: Mac(numeroDoGrupo, 0),
                osUser: "professor",
                agentKeyHash,
                grupo.Id,
                DeviceRole.Professor));

            for (var a = 1; a <= AlunosPorGrupo; a++)
            {
                // PC-101, PC-102... PC-520. Prefixo do grupo garante unicidade
                // (hostname e MAC têm índice único no banco).
                devices.Add(NovoDevice(
                    hostname: $"PC-{numeroDoGrupo}{a:D2}",
                    ip: $"192.168.{numeroDoGrupo}.{a + 100}",
                    mac: Mac(numeroDoGrupo, a),
                    osUser: "aluno",
                    agentKeyHash,
                    grupo.Id,
                    DeviceRole.Aluno));
            }
        }

        AplicarEstadoAtual(devices, rng, agora);
        return devices;
    }

    private static Device NovoDevice(string hostname, string ip, string mac, string osUser, string agentKeyHash, Guid groupId, DeviceRole papel)
    {
        var device = new Device(hostname, ip, mac, "Windows 11 Pro", osUser, agentKeyHash, groupId, deviceInfo: null)
        {
            Id = Guid.NewGuid()
        };

        device.AssignRole(papel);
        return device;
    }

    // Distribui Conexão e Saúde para bater com os big numbers do Dashboard.
    private static void AplicarEstadoAtual(List<Device> devices, Random rng, DateTimeOffset agora)
    {
        // Os 7 offline saem espalhados (índices determinísticos), não os 7 primeiros —
        // senão a tela mostra todo o Lab 1 caído, que não é realista.
        var offline = Enumerable.Range(0, DevicesOffline)
            .Select(i => i * (devices.Count / DevicesOffline))
            .ToHashSet();

        for (var i = 0; i < devices.Count; i++)
        {
            if (offline.Contains(i))
            {
                devices[i].MarkOffline();
                devices[i].LastSeenAt = agora.AddHours(-rng.Next(2, 8));
                continue;
            }

            var cpu = rng.Next(5, 95);
            var ram = rng.Next(20, 95);
            var disco = rng.Next(25, 95);

            // A saúde sai da MESMA regra de domínio que roda em produção —
            // se os limiares mudarem, a massa acompanha sozinha.
            var saude = Device.EvaluateHealth(cpu, ram, disco);

            // Os MESMOS tres numeros que geraram a saude vao para as colunas de
            // percentual — a tela de detalhe mostra exatamente o que classificou
            // a maquina, sem chance de divergir.
            devices[i].MarkSeen(
                agora.AddMinutes(-rng.Next(1, 5)),
                saude,
                uptimeSeconds: rng.Next(3, 8) * 86_400 + rng.Next(0, 23) * 3_600,
                cpuPercent: cpu,
                ramPercent: ram,
                diskPercent: disco);
        }
    }

    private static string Mac(int grupo, int indice) =>
        $"AA:BB:CC:{grupo:X2}:{indice / 256:X2}:{indice % 256:X2}";

    private static List<Alert> MontarAlertas(List<Device> devices, Random rng, DateTimeOffset agora)
    {
        // Um alerta por tipo em cada um dos últimos 7 dias, para o gráfico
        // "Histórico de Alertas" ter as 6 faixas preenchidas em todas as barras.
        var tipos = Enum.GetValues<AlertType>();
        var alertas = new List<Alert>();

        for (var diasAtras = 0; diasAtras < 7; diasAtras++)
        {
            var quantos = rng.Next(4, 12);

            for (var i = 0; i < quantos; i++)
            {
                var device = devices[rng.Next(devices.Count)];
                var tipo = tipos[rng.Next(tipos.Length)];

                alertas.Add(new Alert(device.Id, tipo, MensagemDoAlerta(tipo, device.Hostname))
                {
                    Id = Guid.NewGuid(),
                    // O construtor fixa OccurredAt = agora; aqui espalhamos no tempo.
                    OccurredAt = agora.AddDays(-diasAtras).AddMinutes(-rng.Next(0, 1440))
                });
            }
        }

        return alertas;
    }

    private static string MensagemDoAlerta(AlertType tipo, string hostname) => tipo switch
    {
        AlertType.HighCpu => $"{hostname}: CPU acima de 90% por mais de 5 minutos.",
        AlertType.HighRam => $"{hostname}: memória acima de 90%.",
        AlertType.DiskFull => $"{hostname}: disco com menos de 10% livre.",
        AlertType.HighNetwork => $"{hostname}: tráfego de rede anormal.",
        AlertType.HighPing => $"{hostname}: latência acima de 200 ms até o gateway.",
        AlertType.PendingUpdates => $"{hostname}: atualizações do Windows pendentes.",
        AlertType.ServiceStopped => $"{hostname}: serviço do agente parado.",
        AlertType.HighCpuProcess => $"{hostname}: processo consumindo CPU excessiva.",
        AlertType.MissingProcess => $"{hostname}: processo obrigatório não está rodando.",
        AlertType.FirewallOff => $"{hostname}: firewall do Windows desativado.",
        AlertType.DeviceOffline => $"{hostname}: sem sinal do agente.",
        _ => $"{hostname}: alerta.",
    };

    // Reproduz a tela de Execuções: uma de cada status, incluindo a linha
    // "Executando" que tem botão de parar.
    private static (List<MachineTask>, List<TaskExecutionLog>) MontarTarefas(
        List<Device> devices, Guid criadoPor, DateTimeOffset agora)
    {
        var tarefas = new List<MachineTask>();
        var logs = new List<TaskExecutionLog>();

        var roteiro = new (MachineActionKind Acao, TaskStatus Status, int MinutosAtras, int DuracaoMs)[]
        {
            (MachineActionKind.AtualizacaoWinGet,   TaskStatus.Succeeded, 20,  135_000),
            (MachineActionKind.LimpezaDeDisco,      TaskStatus.Succeeded, 45,  105_000),
            (MachineActionKind.Reinicializacao,     TaskStatus.Failed,    70,   32_000),
            (MachineActionKind.DiagnosticoDeRede,   TaskStatus.Succeeded, 95,  192_000),
            (MachineActionKind.AtualizacaoWindows,  TaskStatus.Running,   10,        0),
            (MachineActionKind.Desligamento,        TaskStatus.Pending,    2,        0),
        };

        for (var i = 0; i < roteiro.Length; i++)
        {
            var (acao, status, minutos, duracao) = roteiro[i];
            var definicao = MachineActionCatalog.Get(acao);
            var quando = agora.AddMinutes(-minutos);

            var tarefa = new MachineTask(definicao.DisplayName, acao, quando, TaskStatus.Pending, criadoPor)
            {
                Id = Guid.NewGuid()
            };

            // Respeita as transições do domínio em vez de atribuir Status direto —
            // se a máquina de estados mudar, a massa quebra e a gente fica sabendo.
            if (status != TaskStatus.Pending)
            {
                tarefa.Queue();
                tarefa.MarkRunning();

                if (status is TaskStatus.Succeeded or TaskStatus.Failed)
                    tarefa.Finish(status == TaskStatus.Succeeded);
            }

            var alvo = devices[i * 7 % devices.Count];

            logs.Add(new TaskExecutionLog(
                definicao.DisplayName,
                status,
                status == TaskStatus.Succeeded ? "Concluído com sucesso." :
                status == TaskStatus.Failed ? "Falha: acesso negado ao executar o comando." : null,
                quando,
                tarefa.Id,
                alvo.Id)
            {
                Id = Guid.NewGuid(),
                DurationMs = duracao > 0 ? duracao : null
            });

            tarefas.Add(tarefa);
        }

        return (tarefas, logs);
    }

    // 15 dias para o toggle "7 dias / 15 dias" do dashboard ter dado nos dois modos.
    private static List<DeviceDailyMetrics> MontarMetricas(List<Device> devices, Random rng, DateTimeOffset agora)
    {
        var metricas = new List<DeviceDailyMetrics>();
        var hoje = DateOnly.FromDateTime(agora.Date);

        // Só as máquinas dos professores: 105 devices x 15 dias = 1575 linhas por
        // boot, o que deixa o seed lento sem acrescentar nada à demonstração.
        var amostra = devices.Where(d => d.Role == DeviceRole.Professor).ToList();

        foreach (var device in amostra)
        {
            for (var diasAtras = 0; diasAtras < 15; diasAtras++)
            {
                metricas.Add(new DeviceDailyMetrics(
                    device.Id,
                    uptimeSeconds: rng.Next(4, 10) * 3_600,
                    peakCpuPercent: rng.Next(40, 99),
                    peakRamPercent: rng.Next(35, 95),
                    peakDiskPercent: rng.Next(30, 92),
                    activeUsersCount: rng.Next(1, 4))
                {
                    Id = Guid.NewGuid(),
                    Date = hoje.AddDays(-diasAtras)
                });
            }
        }

        return metricas;
    }
}
