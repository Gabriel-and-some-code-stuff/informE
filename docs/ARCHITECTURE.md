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
- **ALERTS** (tabela) → núcleo do produto; decidir armazenar vs live-only (sprint de alertas).
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

### 3.6 Checklist de polish (resolver conforme a arch fecha — não tudo na Sprint 1)

1. ✅ **PK = `uuid` único** (INT surrogate dropado; FKs em uuid).
2. **[necessário]** `id_device` em TASK_EXECUTION_LOGS.
3. **[necessário]** Auth do agente: `agent_key_hash` + `key_rotated_at` em DEVICES; tabela EnrollmentToken; status/last_seen do device.
4. **[necessário]** Sessão: `token_hash` + `expires_at` + `last_seen` em SESSIONS.
5. **[sprint 3-4]** Extrair tabela SCRIPTS (predefinido/custom) vs manter `source_script` inline.
6. **[sprint 3]** Tabela ALERTS (tipo/device/msg/timestamps) vs alerta live-only.
7. **[opcional]** `version` em DEVICES_SOFTWARES; padronizar nomes PT/EN (cosmético).

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

## 8. Status das confirmações
1. ✅ **Modelo de Task**: `TASKS` = disparo; `DEVICES_TASKS` = alvos; `TASK_EXECUTION_LOGS` = 1 log/máquina (add `id_device`).
2. ✅ **Reboot/Shutdown** = scripts pré-definidos.
3. ✅ **Schema real recebido** (MySQL, "não perfeito"). Sprint inclui **portar pro Postgres + polir** — §3.5 (porting) e §3.6 (checklist).
4. ✅ **PK = `uuid` único** (INT surrogate dropado; FKs em uuid).
5. Scripts (tabela vs inline) e Alerts (tabela vs live-only) → polir nas sprints 3-4, **não bloqueiam a Sprint 1**.
6. Lib Argon2: default `Isopoh` (trocar se preferir `Konscious`).
