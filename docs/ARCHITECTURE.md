# informE — Documento de Arquitetura + Sprint 1

## Contexto

**informE** = RMM on-premise (sem nuvem) para labs de escola (Etec) e PMEs. Tudo
roda na rede/servidor do cliente. Equipe de 6, **nível júnior em C#** (OOP básico),
prazo de **4 meses** (hoje 10/07 → entrega ~10/11). A nota depende de **entender**
a arquitetura tanto quanto de entregá-la — então este documento define tudo e
ensina o porquê de cada peça.

**Decisões travadas:**
- **UI = MAUI Blazor Hybrid** (moderno, bonito, mais amigável pra front-devs que nunca tocaram C#; e cross-platform de brinde).
- **Agente = Windows-only** (WMI / PerformanceCounter / PowerShell / ServiceController nativos).
- **Auth humana = JWT + Argon2, IDs UUID, SEM ASP.NET Core Identity** (justificativa abaixo).
- **Auth do Agente = enrollment token → chave rotativa** (guardada com DPAPI no disco do agente).
- **Onion Architecture**, 2 soluções (`Host`, `Agent`), conexão via SignalR + REST.

**Filosofia (preguiçoso = eficiente):** usar o que .NET/SignalR/EF já dão de graça.
SignalR resolve reconexão, fila e pub/sub; EF resolve persistência e migrations.
Nada de broker externo, microserviço ou rules-engine.

> **Por que largar o ASP.NET Core Identity?** Ele já vem com PBKDF2 (não Argon2),
> cookies (não JWT) e o schema `AspNetUsers` (não seu `Users` com UUID). Adotá-lo e
> depois trocar as 3 coisas dá MAIS trabalho e esconde o que está acontecendo. Uma
> tabela `Users` + `Argon2` (lib) + `JwtBearer` (pacote oficial) é menos código e
> vocês leem cada linha. `ponytail: sem Identity — vocês controlam hash, token e ID.`

---

## 1. Visão de alto nível — 2 soluções, 3 executáveis

```
        [ Técnico ]                                   [ Endpoint monitorado ]
            │                                                    │
   ┌────────▼─────────┐         REST (HTTPS)          ┌──────────▼──────────┐
   │  informE.Desktop │◄──────  + SignalR    ────────►│  informE.Agent      │
   │  (MAUI Blazor    │       (DashboardHub)          │  (Windows Service)  │
   │   Hybrid)        │                               │                     │
   └────────┬─────────┘                               └──────────┬──────────┘
            │                                                     │
            │   ┌─────────────────────────────────────────┐      │
            └──►│           informE.Server                 │◄─────┘
                │   ASP.NET Core (on-prem):                 │  REST: enrollment,
                │   • REST API (JWT bearer)                 │        inventário
                │   • AgentHub (agentes, chave rotativa)    │  SignalR (AgentHub):
                │   • DashboardHub (operadores, JWT)        │   telemetria/alertas ↑
                │   • Composition root (DI)                 │   comandos ↓
                └────────────────────┬──────────────────────┘
                                     │  EF Core (Npgsql)
                             ┌───────▼────────┐
                             │  PostgreSQL     │  (IDs uuid)
                             └────────────────┘

   Separado (não é o produto on-prem):
   [ landing/ ] Next.js — vitrine de vendas
```

**As duas conexões (pilar CONEXÃO ASSÍNCRONA):**

1. **Agent ↔ Server** — REST/HTTPS para enrollment, upload de inventário, download de scripts; **AgentHub (SignalR/WebSocket)** para o canal vivo: conecta no boot (**RF05**), ping mantém vivo (**RF07**), retry nativo reconecta (**RF06**), *connection registry* por `endpointId` garante **1 conexão só** (**RF08**). Sobe telemetria/alertas; desce comandos.
2. **Desktop ↔ Server** — REST para CRUD/consultas; **DashboardHub (SignalR)** para o server empurrar telemetria/alertas ao vivo pros operadores.

> **Telemetria ao vivo NÃO é persistida** — flui pelo hub e é exibida em tempo real.
> Só **alertas** e **inventário** vão pro banco. `ponytail: sem tabela de métricas;
> se um dia quiserem gráfico histórico, aí sim adiciona uma tabela de séries.`

**Onion — dependências apontam para DENTRO:**
`Contracts` (folha, sem deps) · `Domain` (centro) ← `Application` (casos de uso + *ports*) ← `Infrastructure` (EF, hubs, coletores — implementa os ports) ← `Server`/`Desktop`/`Worker` (composition root, monta o DI).

---

## 2. Estrutura de pastas (monorepo)

```
informE/
├── informE.Host.sln                 # Server + Desktop + camadas + Contracts
├── informE.Agent.sln                # Agent + Contracts (mesmo .csproj nas 2 slns)
├── docker-compose.yml               # postgres p/ dev
├── .github/workflows/ci.yml
├── docs/                            # este doc, DER, diagrama de classes, ADRs
├── src/
│   ├── Shared/informE.Contracts/    # DTOs, nomes de métodos de hub, enums públicos. ZERO deps.
│   ├── Host/
│   │   ├── informE.Domain/          # entidades, enums, regras. Sem deps externas.
│   │   ├── informE.Application/      # casos de uso + interfaces (ports) + validators
│   │   ├── informE.Infrastructure/   # EF Core+Npgsql, repos, hubs, Argon2, JWT, coletor de scripts
│   │   ├── informE.Server/           # ASP.NET Core: controllers + hubs + composition root
│   │   ├── informE.UI/               # Razor Class Library: componentes Blazor compartilhados
│   │   └── informE.Desktop/          # MAUI Blazor Hybrid (o cliente instalado)
│   └── Agent/
│       ├── informE.Agent.Core/          # regras de alerta, thresholds
│       ├── informE.Agent.Application/   # orquestra coletores, trata comandos (ports)
│       ├── informE.Agent.Infrastructure/# WMI/PerfCounter, executor PowerShell, cliente SignalR+HTTP, DPAPI
│       └── informE.Agent.Worker/        # Windows Service (BackgroundService) — o executável
├── tests/
│   ├── informE.Domain.Tests/
│   └── informE.Application.Tests/
└── landing/                         # Next.js
```

> `informE.Contracts.csproj` é adicionado às DUAS .sln e referenciado por
> `ProjectReference`. Um .csproj pode viver em várias soluções — sem NuGet/submódulo.

---

## 3. Modelo de Domínio (o que vocês pediram: entidades, enums, interfaces)

> Todos os `Id` são `Guid` (mapeados como `uuid` no Postgres). Datas em UTC.
> Entidades e enums ficam em **`informE.Domain`** (o centro, sem dependências).

### 3.1 Enums (`informE.Domain/Enums`)

```csharp
public enum UserRole   { Viewer, Admin, SuperAdmin }          // ROLES.name ENUM('V','A','SA')
public enum TaskStatus { Pending, Queued, Running, Succeeded, Failed, Canceled } // hoje VARCHAR no schema
public enum RamType    { DDR3, DDR4 }                          // INFO_DEVICES.ram_type
public enum StorageType{ HD, SSD }                             // INFO_DEVICES.storage_type
public enum EndpointStatus { Online, Offline, Unknown }        // SEM coluna hoje — derivar do connection registry/last_seen
public enum ScriptKind { PowerShell, Batch }                   // só se extrairmos tabela SCRIPTS (hoje inline em TASKS.source_script)
public enum AlertType  { HighRam, DiskFull, HighNetwork, HighPing, PendingUpdates,
                         ServiceStopped, HighCpuProcess, MissingProcess, FirewallOff } // só se criarmos ALERTS
```

### 3.2 Entidades (`informE.Domain/Entities`)

Espelhando o **schema real** (imagem). Coluna "Polir" = furos a resolver no port/arch.

| Tabela (DB) | Entidade C# | Campos reais | Polir |
|---|---|---|---|
| **USERS** | User | id_user, uuid_user, username, email, password_user *(Argon2)*, id_role | — |
| **ROLES** | Role | id_role, uuid_role, name (V/A/SA) | poderia ser só enum em C# |
| **SESSIONS** | Session | id_session, ip_address_login, login_time, is_active(Y/N), id_user | faltam token_hash + expires_at + last_seen (JWT refresh, 3 sessões, revogar 7d) |
| **DEVICES** | Device *(o "Endpoint")* | id_device, uuid_device, hostname, last_ip, mac_address, os, user_os, registered_at, id_info_device, id_group | sem status/last_seen (online/offline); **sem chave do agente** |
| **INFO_DEVICES** | DeviceInfo *(HW, 1-1)* | id_info_device, uuid, cpu, gpu, ram, ram_type, storage, storage_type, bios, id_user | — |
| **GROUPS** | Group | id_group, uuid, group_name, description, active(Y/N), USERS_id_user (dono) | — |
| **TASKS** | Task *(o disparo)* | id_task, task_name, source_script *(inline!)*, date_task, status | script inline (sem biblioteca reusável); sem criador; sem limite simultâneos |
| **DEVICES_TASKS** | (join) | id_device, id_task | os alvos do disparo (M-N) |
| **TASK_EXECUTION_LOGS** | TaskExecutionLog | id_job_exe_log, action_type, status, source_output_log, executed_at, id_task, id_user | **falta id_device** → não liga o log à máquina |
| **SOFTWARES** | Software *(catálogo)* | id_software, name | — |
| **DEVICES_SOFTWARES** | (join) | id_software, id_device | sem versão por instalação |
| **AUDIT_LOGS** | AuditLog | id_audit_log, uuid, action, created_at, ip_address_adm, id_user | — |

**Não existe no schema, mas o produto precisa (fechar na arch do zero):**
- **Chave do agente** (colunas em DEVICES: `agent_key_hash`, `key_rotated_at`) + **EnrollmentToken** (tabela) → sem isso o Agent não autentica (RF05-08 + pilar Segurança).
- **id_device em TASK_EXECUTION_LOGS** → obrigatório pro "1 log por máquina" que confirmamos.
- **Campos de sessão** (token_hash/expires_at/last_seen) → sustentam JWT + 3 sessões + revogar ocioso.
- **ALERTS** (tabela) → ✅ **decidido: persiste** (ver §3.7) — necessário pro gráfico
  "Histórico de Alertas" do dashboard e pra auditoria por dispositivo.
- **SCRIPTS** (tabela) → decidir extrair vs manter inline (sprint de execução).

> **Modelo de Task (✅ CONFIRMADO, mapeado ao schema real):**
> `TASKS` = **o disparo** (qual script, agendamento, status geral). `DEVICES_TASKS` =
> os **alvos** (M-N com DEVICES). `TASK_EXECUTION_LOGS` = **1 registro por máquina**
> (resultado/saída/tempo) — *precisa ganhar `id_device`*. **Reboot/Shutdown = scripts
> pré-definidos** (quando extrairmos a tabela SCRIPTS; hoje o script é inline).

### 3.3 Interfaces / Ports (`informE.Application/Abstractions`)

Portas que a Application define e a Infrastructure implementa (é isso que mantém o
Onion: a Application não conhece EF nem SignalR, só as interfaces).

```csharp
// Segurança
public interface IPasswordHasher { string Hash(string pwd); bool Verify(string pwd, string hash); } // Argon2id
public interface IJwtTokenService { string CreateAccessToken(User u); (string token,DateTime exp) CreateRefreshToken(); }
public interface IAgentAuthenticator { Task<Endpoint?> ValidateKeyAsync(Guid endpointId, string presentedKey); }

// Persistência (portas focadas por agregado; EF as implementa)
public interface IUserRepository            { /* GetByEmail, Add, Update, ListActiveSessions... */ }
public interface IEndpointRepository        { /* Get, ListByGroup, SetStatus, Upsert... */ }
public interface IGroupRepository           { }
public interface IScriptRepository          { }
public interface IMachineTaskRepository     { /* Add task + N execution logs numa transação */ }
public interface IInstalledSoftwareRepo     { /* ReplaceForEndpoint (upsert de inventário) */ }
public interface IAlertRepository           { }
public interface IAuditLogRepository        { }
public interface IUnitOfWork                { Task<int> SaveChangesAsync(); }

// Tempo real / comandos
public interface IEndpointConnectionRegistry { void Register(Guid endpointId,string connId); bool IsOnline(Guid id); /* RF08 */ }
public interface ICommandDispatcher          { Task DispatchAsync(Guid endpointId, CommandDto cmd); } // empurra pro AgentHub
public interface IDashboardNotifier          { Task TelemetryAsync(TelemetryDto t); Task AlertAsync(AlertDto a); } // empurra pro DashboardHub
```

> Tempo: usar o **`TimeProvider`** nativo do .NET (não criar `IClock`) — dá pra testar
> tempo sem inventar abstração. `ponytail: TimeProvider é stdlib.`

### 3.4 Contratos de Hub (`informE.Contracts`)

```csharp
// Server → Agent
public interface IAgentClient { Task RunCommand(CommandDto cmd); Task RotateKey(string newKey); }
// Agent → Server (métodos do AgentHub)
//   ReportTelemetry(TelemetryDto), ReportAlert(AlertDto), ReportCommandResult(ResultDto), Ping()
// Server → Operadores (DashboardHub)
public interface IDashboardClient { Task EndpointStatusChanged(Guid id,EndpointStatus s);
                                    Task TelemetryUpdated(TelemetryDto t); Task AlertRaised(AlertDto a);
                                    Task TaskProgress(Guid taskId, TaskStatus s); }
```

---

### 3.5 Porting MySQL → PostgreSQL (o "jogar pro Postgres" do sprint)

O schema foi desenhado em MySQL. Reexpressar como **EF Core Code-First** (entidades
em C# → EF gera o Postgres + migrations), aplicando:

| MySQL (schema atual) | PostgreSQL / EF | Nota |
|---|---|---|
| `BINARY(16)` (uuid_*) | `uuid` | tipo nativo; `Guid` em C# |
| `INT/BIGINT AUTO_INCREMENT` (id_*) | **dropado** — `uuid` é a PK | `Guid` PK (`gen_random_uuid()`); FKs em uuid |
| `ENUM('Y','N')` (is_active, active) | `boolean` | |
| `ENUM('V','A','SA')` | FK ROLES + `UserRole` em C# | |
| `ENUM('DDR3'..)`, `ENUM('HD'..)` | enum C# (+ smallint) | |
| `TEXT(100)` | `varchar(100)` | `TEXT` no PG não tem tamanho |
| `TIMESTAMP` | `timestamptz` | guardar sempre UTC |

> Manter os nomes do banco (`id_user`, `password_user`…) com **EFCore.NamingConventions**
> (snake_case) — assim o C# fica PascalCase e o Postgres fica igual ao desenho.

---

### 3.6 As camadas da Onion, uma a uma (o que existe hoje no repo)

A regra de ouro: **cada camada só pode referenciar as que estão mais pro centro
dela.** Nunca o contrário. Se um arquivo em `Domain` precisar de algo de
`Infrastructure`, a arquitetura quebrou — é o primeiro erro que a IA de review
(`.github/workflows/ai-review.yml`) foi instruída a caçar.

```
Contracts (folha, zero deps)
   ↑
Domain (o centro — zero deps)
   ↑
Application (depende de Domain + Contracts)
   ↑
Infrastructure (depende de Application + Domain + Contracts)
   ↑
Server / Desktop / Agent.Worker (composition root — depende de tudo)
```

#### `informE.Contracts` — o vocabulário compartilhado

`src/Shared/informE.Contracts/`. **Zero dependências** — nem de `Domain`. É a
única coisa que existe nas DUAS soluções (`Host` e `Agent`) ao mesmo tempo,
porque é o que os dois lados precisam concordar sobre para conversar via
SignalR: nomes de método e formato de dado, sem lógica nenhuma.

- **`Dtos/`** — os "envelopes" que trafegam pela rede. Um DTO nunca é uma
  entidade do Domain reaproveitada; é um objeto simples e achatado, pensado pra
  serialização:
  - `TelemetryDto(DeviceId, CpuPercent, RamPercent, DiskPercent, Timestamp)` —
    o que o Agent manda a cada ping.
  - `CommandDto(TaskId, LogId, Script, Kind)` — o que o Server manda pro Agent
    executar.
  - `CommandResultDto(LogId, Succeeded, Output, ExecutedAt)` — a resposta do
    Agent depois de rodar o comando.
  - `AlertDto(DeviceId, AlertType, Message, Timestamp)` — um alerta pra exibir
    ao vivo no Desktop.
- **`Hubs/`** — as interfaces que descrevem os métodos que cada lado pode
  chamar no outro via SignalR (isso dá autocomplete e checagem de tipo nas duas
  pontas, em vez de nomes de método soltos em string):
  - `IAgentClient` — métodos que o **Server chama no Agent**: `RunCommand`,
    `RotateKey`.
  - `IDashboardClient` — métodos que o **Server chama no operador**:
    `EndpointStatusChanged`, `TelemetryUpdated`, `AlertRaised`, `TaskProgress`.

#### `informE.Domain` — o centro, as regras que não mudam

`src/Host/informE.Domain/`. **Zero dependências externas** — nem EF, nem
ASP.NET, nada. Só C# puro. Se você abrir esse projeto daqui a 2 anos e trocar
Postgres por outro banco, ou SignalR por outra coisa, **nada aqui muda**.

- **`Enums/`** — `UserRole`, `TaskStatus`, `RamType`, `StorageType`,
  `EndpointStatus`, `AlertType`. São os "vocabulários fechados" do domínio.
- **`Entities/`** — os objetos que o negócio entende, cada um com `Guid Id` e
  datas em `DateTimeOffset` (UTC): `User`, `Session`, `Device`, `DeviceInfo`,
  `Group`, `EnrollmentToken`, `MachineTask`, `TaskExecutionLog`, `Software`,
  `AuditLog`. Cada entidade tem só propriedades e as coleções de navegação
  (`ICollection<T>`) — **nenhuma tem `[Table]`, `[Column]` ou qualquer atributo
  de EF**. O Domain não sabe que existe banco de dados.

#### `informE.Application` — os casos de uso e as "portas"

`src/Host/informE.Application/`. Depende de `Domain` + `Contracts`. É aqui que
moram as **interfaces (ports)** que a Infrastructure vai implementar — é assim
que a Application pede "salve isso" ou "gere um hash" sem nunca saber que por
trás tem EF Core ou Argon2.

- **`Abstractions/`** — interfaces de infraestrutura transversal:
  - `IPasswordHasher` — hash/verificação de senha (Argon2id por trás).
  - `IJwtTokenService` — gera access token + refresh token.
  - `IAgentAuthenticator` — valida a chave rotativa que o agente apresenta.
  - `IUnitOfWork` — `SaveChangesAsync()`; hoje o próprio `AppDbContext`
    implementa essa interface diretamente (ver Infrastructure).
  - `IEndpointConnectionRegistry` — quem está online agora (RF08).
  - `ICommandDispatcher` — manda um `CommandDto` pro `AgentHub`.
  - `IDashboardNotifier` — empurra telemetria/alerta/status pro `DashboardHub`.
- **`Abstractions/Repositories/`** — uma interface por agregado, cada uma só
  com os métodos que o caso de uso realmente precisa (não é CRUD genérico):
  `IUserRepository`, `IDeviceRepository`, `IGroupRepository`,
  `IMachineTaskRepository`, `ISoftwareRepository`, `IAuditLogRepository`.

> Ninguém implementa essas interfaces ainda (é a próxima tarefa do Pedro/
> Guilherme) — hoje elas só *existem como contrato*. É esperado que o Server
> ainda não injete nada além do `AppDbContext`.

#### `informE.Infrastructure` — onde a tecnologia mora

`src/Host/informE.Infrastructure/`. Depende de `Application` + `Domain` +
`Contracts`. Aqui — e só aqui — aparecem EF Core, Npgsql, SignalR, Argon2, JWT.
É a única camada que "suja as mãos" com tecnologia concreta.

- **`Persistence/AppDbContext.cs`** — o `DbContext` do EF Core. Implementa
  `IUnitOfWork` (o `SaveChangesAsync` herdado do próprio `DbContext` já bate
  com a assinatura da interface). Expõe um `DbSet<T>` por entidade.
- **`Persistence/Configurations/`** — uma classe `IEntityTypeConfiguration<T>`
  por entidade (Fluent API), uma pra cada uma das 10 entidades — define nome de
  tabela, tamanho de coluna, índices únicos, FKs e comportamento de delete
  (cascade/restrict/set null). É aqui que o "português do banco" (`users`,
  `id_role`) se conecta ao "C# do domínio" (`User.Role`).
- **`Persistence/Migrations/`** — geradas por `dotnet ef migrations add`, nunca
  escritas à mão. Histórico versionado do schema.
- **`Persistence/AppDbContextFactory.cs`** — só existe para o `dotnet ef`
  conseguir montar um `AppDbContext` em tempo de design (gerar/aplicar
  migration) sem precisar subir o `Server` inteiro.
- **`DependencyInjection.cs`** — um método de extensão
  (`AddInfrastructure(config)`) que registra tudo isso no container de DI.
  O `Server` chama essa única linha no `Program.cs` — ele não sabe (nem
  precisa saber) o que tem dentro.
- **Ainda não implementado aqui** (próximas tarefas): `Argon2PasswordHasher`
  (implementa `IPasswordHasher`), `JwtTokenService`, os repositórios
  concretos, e os Hubs (`AgentHub`, `DashboardHub`).

#### `informE.Server` / `informE.Desktop` / `informE.Agent.Worker` — os executáveis

Estes são os **composition roots** — os únicos lugares que conhecem *todas*
as camadas ao mesmo tempo, porque é aqui que o `Program.cs` liga tudo via DI
(`builder.Services.AddInfrastructure(...)`, etc.). Eles não têm regra de
negócio própria — só orquestram: recebem uma requisição HTTP/SignalR, chamam
um caso de uso da Application, devolvem a resposta.

> **Por que essa separação importa na prática:** se amanhã alguém decidir
> trocar Postgres por SQL Server, só `Infrastructure` muda. Se decidir trocar
> Argon2 por outra lib de hash, só a implementação de `IPasswordHasher` muda —
> ninguém que chama `IPasswordHasher.Hash(senha)` no `Application` percebe a
> diferença. Essa é a promessa do Onion: **mudança de tecnologia não vaza pro
> resto do sistema.**

### 3.6 Checklist de polish (resolver conforme a arch fecha — não tudo na Sprint 1)

1. ✅ **PK = `uuid` único** (INT surrogate dropado; FKs em uuid).
2. **[necessário]** `id_device` em TASK_EXECUTION_LOGS.
3. **[necessário]** Auth do agente: `agent_key_hash` + `key_rotated_at` em DEVICES; tabela EnrollmentToken; status/last_seen do device.
4. **[necessário]** Sessão: `token_hash` + `expires_at` + `last_seen` em SESSIONS.
5. **[sprint 3-4]** Extrair tabela SCRIPTS (predefinido/custom) vs manter `source_script` inline.
6. ✅ **Resolvido:** Tabela ALERTS persiste (ver §3.7) — necessária pro gráfico de
   histórico e auditoria por dispositivo.
7. **[opcional]** `version` em DEVICES_SOFTWARES; padronizar nomes PT/EN (cosmético).

---

### 3.7 Métricas diárias, Alertas e crescimento da rede (dashboard + gráficos)

O wireframe do dashboard pede: big numbers com delta (`+3`, `-2`), um toggle de
período (`7 dias` / `15 dias`) e um gráfico empilhado "Histórico de Alertas" por
tipo. Isso levantou a pergunta: uma entidade `History` única guardando uptime,
picos de CPU/RAM/disco, contagem de alertas, contagem de execuções, hostname, IP
e crescimento da rede — tudo junto?

**Não.** Esses campos têm **grãos diferentes** (o que uma linha representa), e
misturar grão numa tabela só cria uma tabela larga, cheia de coluna às vezes
nula, com índices que servem bem pra uma pergunta e mal pra outra. A solução
manteve os dois princípios (EF Code-First, Onion) mas separou por grão real:

#### `DeviceDailyMetrics` — grão: por device, por dia

```csharp
public class DeviceDailyMetrics
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateOnly Date { get; set; }
    public int UptimeSeconds { get; set; }
    public float PeakCpuPercent { get; set; }
    public float PeakRamPercent { get; set; }
    public float PeakDiskPercent { get; set; }
    public int ActiveUsersCount { get; set; }
}
```

Uptime, picos de recurso e usuários ativos são **o mesmo grão** (uma máquina,
um dia) — cabem na mesma linha sem violar nada. O **agente** calcula isso
localmente (contador de uptime + máximos observados desde a meia-noite) e
manda um upsert incremental pelo `AgentHub` (`DailyMetricsDto` em
`informE.Contracts`) — não espera o dia fechar, então uma queda do agente no
meio do dia só perde o intervalo desde o último envio, não o dia inteiro.
`UNIQUE(DeviceId, Date)` garante 1 linha por combinação.

#### `Alert` — grão: por device, por ocorrência

```csharp
public class Alert
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public AlertType Type { get; set; }     // enum já existente em Domain.Enums
    public string Message { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
```

Diferente de telemetria (que continua **ao vivo, nunca persistida** — decisão
mantida), o alerta passa a ser **persistido**. É o que sustenta:
- o gráfico "Histórico de Alertas" (stacked bar por dia/tipo): `GROUP BY
  DATE(occurred_at), type` direto nessa tabela — sem tabela agregada extra;
- auditoria "quantos alertas esse device teve" — mesma query, sem `JOIN` com
  `DeviceDailyMetrics`.

**Contagem de alertas por dia e contagem de execuções por dia (pergunta 4 da
proposta original) não viram coluna em lugar nenhum** — são derivadas por
`GROUP BY` em `Alert` e em `TaskExecutionLog` (que já existe) no momento da
consulta. Duplicar esse número seria redundância sem ganho: as tabelas são
pequenas o bastante (escala de escola/PME) para o agregado ser instantâneo.

#### `NetworkGrowthSnapshot` — grão: por dia (tenant inteiro, sem device)

```csharp
public class NetworkGrowthSnapshot
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public int TotalDevices { get; set; }
    public int TotalGroups { get; set; }
}
```

Este é o único caso que genuinamente não cabe nas tabelas por-device — "total
de agents e grupos" é uma métrica do tenant inteiro, uma linha por dia. Um job
diário grava o snapshot. **Opcional**: só compensa se o produto quiser mesmo
"há 30 dias tínhamos 80 máquinas, hoje 105" — se bastar o número atual, é só
`COUNT(*)` ao vivo, sem histórico nenhum.

#### O que ficou fora (YAGNI)

- **Hostname/IP no histórico diário** — ficam como estão hoje, valor atual em
  `Device.Hostname`/`Device.LastIp`. Histórico de renomeação ou de troca de IP
  por DHCP é evento raro; se algum dia for pedido de verdade, é uma tabela de
  evento à parte (mesmo padrão do `Alert`), não uma coluna nesta.

#### Sparkline e filtro de data (7/15 dias)

Uma vez que `DeviceDailyMetrics`/`Alert` têm grão diário, o toggle do wireframe
é só camada de consulta: `WHERE Date BETWEEN @inicio AND @fim` — não precisa de
estrutura nova além do que já foi criado aqui.

#### Retenção — e o gancho de precificação

O job de purga (`IDeviceDailyMetricsRepository.PurgeOlderThanAsync`) apaga
linhas mais velhas que a janela de retenção configurada. Essa janela (ex.: 15
dias) é um valor de configuração por tenant — o que a torna, de graça, uma
alavanca natural de plano: *"Básico: 15 dias de histórico"* vs *"Pro: 90
dias"* é o mesmo mecanismo, só lendo um número diferente.

---

## 4. Segurança — como implementar (pilares SEGURANÇA + INTEGRIDADE)

- **Senhas: Argon2id** via lib mantida (`Isopoh.Cryptography.Argon2` ou `Konscious.Security.Cryptography.Argon2`). `IPasswordHasher` guarda a string encoded (inclui salt+params). Nunca hash na mão.
- **Login: JWT**. Access token curto (~15 min) com claim de `role`; **refresh token** persistido (tabela RefreshToken). `[Authorize(Roles="Admin")]` lê o claim.
- **Sessões**: máx. 3 refresh tokens ativos/usuário (no 4º login, revoga o mais antigo); não usado em 7 dias → expira. Um `BackgroundService` varre e revoga ociosos.
- **IDs UUID**: `Guid` PK em tudo, evita enumeração sequencial. `ponytail: Guid aleatório basta na escala de escola; não se preocupar com fragmentação de índice.`
- **Agente**: admin gera EnrollmentToken (uso único, expira) → agente chama `/enroll` → server cria Endpoint + emite chave por-máquina → agente guarda com **DPAPI**. Chave rotaciona periodicamente (server manda `RotateKey` pelo hub). AgentHub e REST validam via `IAgentAuthenticator`.
- **HTTPS** obrigatório mesmo na LAN.
- **Integridade de comando**: persistir `MachineTask` + N `TaskExecutionLog` (Pending) numa **transação** ANTES de despachar; se o server cair, o estado sobrevive e reconcilia no reconnect.
- **Fila + limite de simultâneos**: `System.Threading.Channels` (bounded). `ponytail: Channel como fila; virar tabela outbox só se precisar durar após restart.`

---

## 5. Roadmap macro (4 meses, sprints de 2 semanas ≈ 8 sprints)

| Sprint | Janela | Foco |
|---|---|---|
| **1** | 10/07–24/07 | **Fundacional / walking skeleton** (detalhado abaixo) |
| 2 | 24/07–07/08 | Enrollment completo + inventário HW/OS + lista de endpoints/grupos na UI |
| 3 | 07/08–21/08 | Inventário de software + alertas (RAM/disco/CPU) ponta a ponta + notificações |
| 4 | 21/08–04/09 | Execução de scripts em massa (fila + limite) + logs de execução |
| 5 | 04/09–18/09 | Agendamento + reboot/shutdown + auditoria + histórico de atividades |
| 6 | 18/09–02/10 | Resto dos alertas + dashboard geral (big numbers + gráficos) + views Grupos/Labs |
| 7 | 02/10–16/10 | Hardening de segurança (rotação de chave, revogação de sessão), QA, empacotar instaladores |
| 8 | 16/10–30/10 | Testes finais, docs, vitrine Next.js, folga p/ imprevistos |

---

## 6. Sprint 1 — Fundacional (o walking skeleton)

**Meta única e verificável:** um agente instalado numa VM Windows faz **enrollment**,
aparece **online** no Desktop, e seu **%CPU ao vivo** atualiza no dashboard. Login
JWT funciona. CI verde. `docker compose up` sobe Postgres.

Provado isso, todo o resto é preencher o esqueleto em paralelo. **Ninguém constrói
feature larga antes do esqueleto fechar.**

### Tarefas por pessoa (papéis do deck)

**Gabriel Vasconcellos — dono da espinha (Back-End · Redes · DevOps)**
- Monorepo, 2 `.sln`, todos os projetos Onion vazios + `Contracts` nas duas.
- `docker-compose.yml` (Postgres) + `ci.yml`.
- Contrato do **AgentHub** + *connection registry* por `endpointId` (RF05–08).
- **Estudar:** Onion (camadas/dependências), SignalR Hub + Groups, GitHub Actions.
- **Como seguir:** montar o esqueleto e travar as assinaturas do hub com o Augusto no dia 1.

**Pedro Ribeiro — Server + auth (Back-End · DevOps/QA)**
- `informE.Server`: composition root (DI), API skeleton, `/enroll` (token → chave), `IAgentAuthenticator`.
- Auth JWT: login, `IPasswordHasher` (Argon2), `IJwtTokenService`, refresh token + regra de 3 sessões.
- **Estudar:** JWT bearer no ASP.NET Core, DI, lib Argon2.
- **Como seguir:** parear com Guilherme (schema User/RefreshToken/Endpoint) e Gabriel (hub).

**Augusto Marmiroli — o Agente (Back-End · Redes · Docs)**
- `informE.Agent.Worker`: Windows Service + cliente SignalR que conecta no AgentHub, manda ping e **1 amostra de %CPU** (`PerformanceCounter`); chave guardada com DPAPI.
- **Estudar:** .NET Worker/Windows Service, `SignalR.Client`, `PerformanceCounter`, DPAPI (`ProtectedData`).
- **Como seguir:** consumir os contratos do Gabriel; testar numa VM Windows apontando pro Server local.

**Guilherme Faggian — dados (Back-End · Banco de Dados)**
- **Portar o schema (imagem) pra PostgreSQL via EF Core Code-First + migrations** (§3.5). Sprint 1 = só o necessário ao skeleton: `User`, `Role`, `Session`, `Device`, `Group`, `DeviceInfo`, `EnrollmentToken`. Já resolver os furos necessários desse recorte: colunas de auth/last_seen em `Device`, campos de token/expiry em `Session`.
- **Estudar:** EF Core Code-First + migrations, Npgsql, EFCore.NamingConventions (snake_case p/ casar `id_user` etc.), tipos `uuid`/`timestamptz`/`boolean`.
- **Como seguir:** entregar `DbContext` + entidades cedo; alinhar com Pedro (auth) e a decisão de PK.

**Bruna Garcia — shell da UI (Front-End · Docs)**
- `informE.Desktop`: bootar **MAUI Blazor Hybrid**, tela de **login** consumindo o JWT, layout/navegação base + RCL `informE.UI`.
- **Estudar:** MAUI Blazor Hybrid, componentes Blazor, `HttpClient` tipado guardando o token JWT.
- **Como seguir:** parear com Eduardo; login primeiro, depois a casca onde o dashboard encaixa.

**Eduardo Valim — dashboard ao vivo + vitrine (Front-End · Docs)**
- Componente que assina o **DashboardHub** e mostra %CPU ao vivo de 1 endpoint + big number "endpoints online".
- Scaffold do `landing/` Next.js (baixa prioridade).
- **Estudar:** `SignalR.Client` no Blazor, binding reativo, Next.js básico.
- **Como seguir:** consumir o DashboardHub; começar com 1 endpoint e generalizar.

### Definition of Done (Sprint 1)
1. `git clone` + `docker compose up` → Postgres no ar; `dotnet ef database update` aplica migrations.
2. Admin loga no Desktop (JWT, Argon2 verifica a senha).
3. Agente numa VM Windows faz enrollment e aparece **online**.
4. %CPU do agente atualiza **ao vivo** no dashboard (Agent → AgentHub → Server → DashboardHub → Desktop).
5. Matar/reconectar o agente → some/volta online (RF06/RF08 provados).
6. CI verde nas 2 soluções.

---

## 7. Verificação (como provar que funciona)
- **DB:** `docker compose up` + `dotnet ef database update`; conferir tabelas no Postgres.
- **Server:** `dotnet run`; `/enroll` via REST client retorna chave; login retorna JWT.
- **Agent:** rodar `informE.Agent.Worker` numa VM apontando pro Server; ver conexão no registry e amostras no log do hub.
- **Ponta a ponta:** abrir Desktop, logar, ver endpoint online e %CPU subindo ao vivo ao forçar carga na VM.
- **Resiliência:** matar o agente → some; reiniciar → reconecta sozinho.
- **Testes:** `dotnet test` — `Domain.Tests` cobre 1 regra de alerta (ex.: RAM > threshold dispara); `Application.Tests` 1 caso de uso com ports mockados.

---

## 8. Ambiente de desenvolvimento (sem admin)

O script `setup-dev.ps1` na raiz do repo instala tudo sem privilégio de administrador:

```powershell
powershell -ExecutionPolicy Bypass -File setup-dev.ps1
```

| Ferramenta | Instalação | Onde fica |
|---|---|---|
| .NET 10 SDK | `dotnet-install.ps1` (Microsoft oficial) | `%LOCALAPPDATA%\dotnet` |
| MAUI workload | `dotnet workload install maui-windows` | dentro do SDK do usuário |
| gh CLI | zip do GitHub Releases, sem instalador | `$HOME\.local\gh\<versao>\bin` |
| Docker Desktop | **precisa de TI / já instalado** | — |
| PostgreSQL | via `docker compose up -d` | container `informe-postgres` |

> **Postgres roda em Docker** — não precisa instalar separado. Docker Desktop
> é pré-requisito (instalar uma vez com admin da TI). Depois de ter o Docker,
> `docker compose up -d` sobe o banco e `docker compose down` derruba, sem admin.

Após rodar o script, **abra um novo terminal** (PATH foi atualizado para a conta do usuário, mas não recarrega na sessão atual).

---

## 9. Pipeline de CI/CD

Dois jobs paralelos em `.github/workflows/ci.yml`:

| Job | Runner | O que faz |
|---|---|---|
| `build-agent` | ubuntu-latest | Compila `informE.Agent.slnx` (~1 min, sem MAUI) |
| `build-host` | windows-latest | Compila `informE.Host.slnx` + roda testes; workload MAUI **cacheado** |

Na primeira execução o cache do workload MAUI ainda não existe — leva ~5 min.
A partir da segunda, o step de restauração é pulado e o job cai para ~1–2 min.

### Revisão de PR por IA (`.github/workflows/ai-review.yml`)

A cada Pull Request, o diff em `.cs/.razor/.csproj` (até 8 KB) é enviado ao
**GitHub Models** (GPT-4o mini) que aponta até 5 problemas reais — violações de
Onion, segurança, bugs. Custo: **$0** — usa o `GITHUB_TOKEN` automático do
Actions, sem secret extra, sem conta Anthropic.

> A IA revisa; o time decide. Não rejeita o PR automaticamente.

> ⚠️ **Por que aparece um ❌ em vermelho no Actions mesmo sem PR aberto:** o
> `ai-review.yml` só dispara em `pull_request` (abrir/atualizar). Como o time
> ainda está commitando direto na `master` (sem passar por PR), o GitHub cria
> uma entrada "failure" **fantasma** pra esse workflow em todo push — mas com
> **zero jobs executados** (confirmável em Actions → clicar na run → "0 jobs").
> Não é revisão barrando nada; é só o GitHub avisando que o evento não bateu
> com o trigger. Some sozinho assim que o time passar a trabalhar com Pull
> Requests (branch → PR → merge) em vez de push direto na `master`.

---

## 10. Status das confirmações
1. ✅ **Modelo de Task**: `TASKS` = disparo; `DEVICES_TASKS` = alvos; `TASK_EXECUTION_LOGS` = 1 log/máquina (add `id_device`).
2. ✅ **Reboot/Shutdown** = scripts pré-definidos.
3. ✅ **Schema real recebido** (MySQL, "não perfeito"). Sprint inclui **portar pro Postgres + polir** — §3.5 (porting) e §3.6 (checklist).
4. ✅ **PK = `uuid` único** (INT surrogate dropado; FKs em uuid).
5. Scripts (tabela vs inline) e Alerts (tabela vs live-only) → polir nas sprints 3-4, **não bloqueiam a Sprint 1**.
6. Lib Argon2: default `Isopoh` (trocar se preferir `Konscious`).
