# Host — Status das 3 camadas (Server / Application / Infrastructure)

> Atualizado em **28/08/2026**. Fontes: `docs/api-server.md`, `docs/ARCHITECTURE.md`,
> leitura direta de `src/Host/` e do DI (`DependencyInjection.cs`). Branch de trabalho: `prs-acumuladas`.

> ⚠️ **Parcialmente superado em 28/08 (noite).** Este levantamento foi escrito antes do merge
> da revisão do servidor (`428c786`) e continua valioso como diagnóstico — a análise das três
> lacunas por camada estava certa e é exatamente o que foi atacado. O que mudou desde então:
>
> - as **10 rotas** viraram **26**; o "MVP ausente" listado aqui (refresh, logout, sessões,
>   CRUD de users, detalhe de task, grupos) **foi entregue**;
> - a Application ganhou `RefreshTokenUseCase` e `UpdateUserProfileUseCase` (14 use cases);
> - a Infrastructure ganhou reset de conexões no boot e `HasPendingLogsAsync`.
>
> **Atualizado em 01/09:** a rotação de chave deixou de estar inerte — o host agora
> entrega `KeyRotationSweeper` (ver §4). É job de fundo, **não** endpoint, como o desenho
> previa; as duas pontas (`RotateKeyAsync` no repositório + `RotateKey` no agente) já
> existiam, faltava o disparo.
>
> Continuam abertos, como este documento previu: rotas de dashboard/alerta/métrica, purga de
> auditoria e reentrega de comando offline.
>
> Estado atual em **`docs/situacao-atual.md`** e **`docs/plano-revisao-servidor.md`**.

## Visão geral

| Camada | Estado | O que falta (resumo) |
|---|---|---|
| **informE.Server** | **10 rotas** REST + 2 hubs funcionando | **~24 rotas ausentes** (MVP: refresh/logout/sessões/users/tasks-detalhe; deferidas: dashboard/grupos) |
| **informE.Application** | 12 use cases + ports completos | Leitura de dashboard, orquestração de rotação de chave, kick em tempo real |
| **informE.Infrastructure** | Persistência + realtime + security + sweeps fechados | Purga de auditoria, time-out de tarefa pendente (rotação de chave entregue em 01/09 — §4) |

As três lacunas da **Application** têm raiz no mesmo lugar: a camada está 100% orientada a
comando/escrita e nunca pediu "leia" ao banco. E as três da **Infraestrutura** são
"ausência de evento" — exatamente os casos que a doc manda tratar como *background job*.

---

## 1. informE.Server — composição root (REST + SignalR)

Camada executável (`src/Host/informE.Server/`). Não tem regra de negócio: registra DI, mapeia
rotas e hubs, traduz erro → HTTP. É o que `Program.cs` faz em ~90 linhas.

### ✅ O que já foi feito

**Endpoints REST** — todos atrás de `Authorization: Bearer`, exceto `/auth/login`, `/agent/enroll` e `/`:

| Método | Rota | Quem pode | O quê |
|---|---|---|---|
| `POST` | `/auth/login` | anônimo | Emite access (15 min) + refresh (7 d) |
| `POST` | `/users` | Admin, SuperAdmin | Cria usuário (papel decide quem) |
| `GET` | `/devices` | autenticado | Lista com filtros `?grupoId/status/busca` + resumo |
| `GET` | `/devices/{id}` | autenticado | Um equipamento |
| `GET` | `/actions` | autenticado | Catálogo de ações (dropdown) |
| `POST` | `/tasks` | autenticado | Dispara ação em N máquinas/grupos |
| `GET` | `/tasks` | autenticado | Execuções recentes (grão por máquina) |
| `POST` | `/tasks/{id}/cancel` | autenticado | Cancela execução |
| `POST` | `/agent/enroll` | **anônimo** | Registra máquina (token uso único, 2 h) |
| `POST` | `/admin/enrollment-tokens` | Admin, SuperAdmin | Emite token de registro |

**Hubs SignalR** — mapeados em `Program.cs`:

| Hub | Rota | Autenticação |
|---|---|---|
| `AgentHub` | `/hubs/agent` | Chave rotativa na query do handshake (sem `[Authorize]`; valida no `OnConnectedAsync`) |
| `DashboardHub` | `/hubs/dashboard` | JWT na query (`WebSocket` não manda header) |

**Infra da API:**
- Erros de negócio → `ProblemDetails` (RFC 7807), mapeamento consistente (400/401/403/409/500), stack só no log do servidor.
- CORS dev (`WithOrigins` localhost 5000/5001/5173/5021 + `AllowCredentials` — obrigatório pro SignalR).
- `/scalar/v1` em Development (UI que lê `/openapi/v1.json`; `HideModels` ligado).
- Bootstrap do banco no boot: migrate + seed de dev (`DatabaseBootstrapper`).
- Limite de dispositivos corrigido para contar **device, não sessão** (devia ter sido resolvido na `politica-login-sessao`; o primeiro rascunho trancava o usuário ao alternar de navegador).

### ❌ O que falta

O `Program.cs` só mapeia **10 rotas hoje**. As telas (`telas-11-09.md`), os DTOs de
contrato e os requisitos pedem bem mais — há **~24 rotas ausentes** divididas em três
grupos. Alguns use cases já existem na Application e é "só rotear"; outros exigem
use case novo (Application) ou entidade/contrato novo (Infra/Shared).

#### A. MVP real — precisa existir (falta)

Use cases **já prontos** e sem rota (rotear + pouco mais):

| Rota faltante | Por trás | Status do use case |
|---|---|---|
| `POST /auth/refresh` | renovar access token | `refreshToken` já é emitido/persistido, nada o consome (contrato novo) |
| `POST /auth/logout` | revogar a própria sessão | `RevokeSessionUseCase` ✅ |
| `DELETE /sessions/{id}` | "desconectar sessão/dispositivo" (comentário Figma #242; Admin revoga de terceiro) | `RevokeSessionUseCase` ✅ |
| `GET /sessions` | tela Meu Perfil lista os dispositivos logados | use case de leitura **falta** |
| `PUT /users/{id}/status` | Ativar/desativar (coluna Status da Adm. de Contas) | `SetUserActiveUseCase` ✅ |
| `PUT /users/{id}/role` | mudar papel (matriz da política) | `ChangeUserRoleUseCase` ✅ |
| `GET /users` | lista de usuários (Adm. de Contas exige) | use case de leitura **falta** |
| `GET /users/{id}` | detalhe/edição | use case de leitura **falta** |
| `PUT /users/me` | Meu Perfil (nome, email, senha) | parcial — `User` não tem Cargo/Org/Fuso |
| `GET /tasks/{id}` + logs | "clique no estado → logs → scripts" | use case de leitura **falta** |

Marginal, transversal: **paginação** — `/devices` e `/users` devolvem tudo (`DeviceEndpoints` não pagina nem filtra por limite além do `:guid`).

#### B. Deferida (mock hoje — vira endpoint quando sair do mock)

`telas-11-09.md` marca **Dashboard e Grupos como 100% fictícios** para o MVP. Quando
saírem do mock, essas rotas precisam existir — e os repositórios/contratos que as
alimentariam já estão prontos na Infra:

| Rota faltante | Fonte de dados (Infra) |
|---|---|
| `GET /dashboard/summary` (big numbers) | `DeviceRepository` + `IAlertRepository` |
| `GET /alerts` / `GET /alerts/{id}` | `AlertRepository` ✅ |
| `GET /alerts/history` (histograma 7/15 d) | `AlertRepository` + `AlertCategoryMap` (Domain) |
| `GET /metrics/daily` | `DeviceDailyMetricsRepository` ✅ (consome `DailyMetricsDto`) |
| `GET /metrics/network-growth` | `NetworkGrowthRepository` ✅ |
| `GET /devices/{id}/software` (inventário) | `SoftwareRepository` ✅ |
| `GET /devices/{id}/alerts` | `AlertRepository` ✅ |
| `GET /groups` + `POST`/`GET {id}`/`PUT`/`DELETE` + associar devices | `GroupRepository` ✅ (`DeviceRole` no Domain) |

#### C. Fora do MVP / depende de decisão

| Item | Motivo |
|---|---|
| `POST /auth/request-password-reset` / `reset-password` | `RequestPasswordReset`/`ResetPassword` prontos, e `SmtpEmailSender` existe — mas **decisão aberta sobre SMTP na rede da Etec** (comentário #227): self-service vs "procure um admin" |
| `POST /tasks/{id}/interrupt` | distinto de cancelar; exigiria método novo no `IAgentClient`. Fora do escopo |
| Chamados/Tickets (7 pontos na UI) | mock, **zero backend, decisão tomada** |
| 2FA, histórico de processos 1–2 h, excluir/purga de logs | Fase 2 / entidade nova / ver §3 infra |

**Padrão:** o Server sozinho **não** resolve quase nada disso — boa parte é "rotear o
que a Application já tem"; o resto depende de use case de leitura (Application) ou de
contrato/entidade (Shared/Infra). As seções §2 e §3 listam o que cada camada precisa
para destravar o grupo A e o grupo B.

---

## 2. informE.Application — casos de uso e ports

`src/Host/informE.Application/`. Depende só de `Domain` + `Contracts`. Aqui moram os use cases
(Orquestração) e as **interfaces/ports** que a Infra implementa.

### ✅ O que já foi feito

**Use cases (12)** com todos registrados no DI:

| Use case | Status |
|---|---|
| `LoginUseCase` | ✅ (limite de dispositivos por `DeviceLabel`, timestamps UTC) |
| `CreateUserUseCase` | ✅ (papel decide quem — regra vive aqui, não no endpoint) |
| `CreateEnrollmentTokenUseCase` | ✅ |
| `EnrollDeviceUseCase` | ✅ (token uso único + hash Argon2id da chave) |
| `DispatchTaskUseCase` | ✅ (sem duplicar device presente em deviceIds e groupIds) |
| `CancelTaskUseCase` | ✅ (cancelamento real no agente exige método novo no contrato — ver `agente.md`) |
| `RecordCommandResultUseCase` | ✅ |
| `RecordDeviceHeartbeatUseCase` | ✅ |
| `RevokeSessionUseCase` | ✅ (revoga no banco; ainda não notifica em tempo real — ver lacuna 3) |
| `SetUserActiveUseCase` | ✅ (sem rota — §1) |
| `ChangeUserRoleUseCase` | ✅ (sem rota — §1) |
| `RequestPasswordResetUseCase` / `ResetPasswordUseCase` | ✅ (sem rota — §1) |

**Ports (interfaces)** — todos com implementação concreta registrada no DI da Infra, sem órfãos:
- Serviços: `IPasswordHasher`, `IJwtTokenService`, `IAgentAuthenticator`, `IUnitOfWork`
  (o próprio `AppDbContext` é o `IUnitOfWork`), `IEndpointConnectionRegistry`,
  `ICommandDispatcher`, `IDashboardNotifier`, `IEmailSender`.
- Repositórios: `IUserRepository`, `IDeviceRepository`, `IGroupRepository`,
  `IMachineTaskRepository`, `ISoftwareRepository`, `IAuditLogRepository`,
  `IEnrollmentTokenRepository`, `IDeviceDailyMetricsRepository`, `IAlertRepository`,
  `INetworkGrowthRepository`, `IPasswordResetTokenRepository`.

**Modelos** (requests/responses) e **exceções de domínio-aplicação** (`InvalidCredentials`,
`AccountDisabled`, `DeviceLimitReached`, `EnrollmentTokenInvalid`, `ForbiddenRoleAssignment`,
`InvalidResetToken`) — traduzidas em HTTP pelo `ExcecaoParaHttpHandler` do Server.

**Testes** (`tests/informE.Application.Tests`): `Login`, `CreateUser`, `DispatchTask`,
`EnrollDevice`, `RevokeSession`, `ChangeUserRole`, `PasswordReset`.

### ❌ O que falta

1. **Use cases de leitura — quase nenhum existe.** A camada é 100% comando/escrita; não há
   leitura de sessão, usuário, tarefa nem dashboard:
   - **Sessões:** sem `ListSessionsUseCase` (tela Meu Perfil / grupo A do §1);
   - **Usuários:** sem `ListUsersUseCase` / `GetUserUseCase` (Adm. de Contas);
   - **Tarefa:** sem `GetTaskLogsUseCase` / detalhe de execução ("clique no estado → logs");
   - **Dashboard/alertas/métricas:** sem `ListAlertsUseCase`, `GetDailyMetricsUseCase`,
     resumo/growth — os repositórios (`IAlertRepository`, `IDeviceDailyMetricsRepository`,
     `INetworkGrowthRepository`) já estão implementados na Infra, mas a Application nunca pede
     leitura.
   **É a causa raiz das lacunas §1-A e §1-B**: `DeviceEndpoints`/`ExecutionEndpoints` injetam
   `IDeviceRepository`/`IMachineTaskRepository` direto e montam a resposta (ex.: o `resumo`)
   no próprio endpoint — só porque não existe use case de leitura pra chamar. Não é violação
   de camada (Server→Application port é permitido), é lógica de negócio fora do lugar.
2. **Quem dispara a rotação de chave do agente.** `IDeviceRepository.RotateKeyAsync` e o canal
   `IAgentClient.RotateKey` existem, o agente sabe receber a troca — mas nenhum use case gera a
   chave nova. A doc é explícita: *"o Host ainda não dispara"* (`agente.md:112`). Falta o
   `RotateAgentKeyUseCase` (e a política de intervalo que o chama — §3).
3. **Kick em tempo real.** `IDashboardClient` (contrato, `src/Shared/`) não tem `SessionRevoked()`.
   Quem revoga a sessão descobre depois, por poll. Em aberto explícito em `politica-login-sessao.md:261`.

---

## 3. informE.Infrastructure — implementações concretas

`src/Host/informE.Infrastructure/`. Depende de `Application` + `Domain` + `Contracts`. EF Core,
SignalR, Argon2, SMTP, jobs. É a maior camada, e a mais preenchida.

### ✅ O que já foi feito

**Persistence**
- `AppDbContext` + `AppDbContextFactory`, naming snake_case.
- 14 configurações de entidade (Fluent API), 7 migrations aplicáveis no boot:
  `InitialCreate`, `AddDailyMetricsAlertsGrowth`, `AlinharDominioComTelas`, `SessionDeviceLabel`,
  `UptimeCodigosLegiveis`, `PasswordResetToken`, `CamposOpcionaisBiosEMensagem` (correção: BIOS e
  mensagem viraram opcionais — quebrava inventário em máquina sem firmware legível).
- 11 repositórios concretos; todos os ports da Application têm implementação registrada no DI.

**Realtime** — os hubs ficam aqui (não no Server) porque `SignalRCommandDispatcher` precisa de
`IHubContext<AgentHub>`:
- `AgentHub` (autentica por chave rotativa no handshake) e `DashboardHub` (`[Authorize]`),
- `EndpointConnectionRegistry` (Singleton, `ConcurrentDictionary`),
- `SignalRDashboardNotifier` / `SignalRCommandDispatcher` (adaptadores via `IHubContext`).

**Security**
- `PasswordHasher` (Argon2id — `IPasswordHasher`), `JwtTokenService` (access + refresh),
  `JwtOptions` (bind da seção `Jwt`) e `AgentAuthenticator` (valida a chave rotativa).

**BackgroundJobs** — varreduras periódicas (`MonitoringOptions`):
- `DeviceOfflineSweeper` — RN03: offline após `OfflineThresholdMinutes` (90 min padrão, múltiplo do snapshot de 30 min);
- `ExpiredSessionSweeper` — sessões inativas.
- `KeyRotationSweeper` — RF13 desde 01/09: rotaciona a chave de device antiga na idade `KeyMaxAgeDays`; ver §4.

**E-mail**: `SmtpEmailSender` + `SmtpOptions` (base do reset de senha).

**Seeding**: `DatabaseBootstrapper` (migrate no boot) + `SeedData` (só Development, determinístico,
105 devices + 6 usuários com `informe123`).

### ❌ O que falta

1. **Purga de audit log.** `ARCHITECTURE.md:542` marca "Purga automática: **ainda não**". Os
   sweeps existem para device offline e sessão expirada, mas nada de retenção/expurgo do
   `AuditLog` — cresce sem limite.
2. **Time-out de tarefa pendente.** Comando despachado com o agente offline deixa o
   `TaskExecutionLog` em `Pending` (o agente ainda não pede os pendentes na reconexão —
   `agente.md:239`). O contraponto no Host também não existe: **nenhum job marca como falha**
   a tarefa que nunca rodou. Sem fila de reconexão num lado e expiração no outro, o log fica
   `Pending` eterno.

---

## Prioridade sugerida (ordem de ataque)

~~1. **Application — leituras de dashboard**~~ — segue em aberto, é a lacuna de maior impacto.
~~2. **Server — `/auth/refresh`**~~ — ✅ entregue.
~~3. **Rotação de chave**~~ — ✅ entregue em 01/09 (sem use case na Application; é job de Infra — §4).
4. **Server — rotas restantes** (`logout`, `set-user-active`, `change-role`, reset de senha):
   ~~use cases já existem, é só rotear~~ — ✅ entregue.
5. **Infra — purga de auditoria e time-out de tarefa pendente** (baixo risco, alto valor
   operacional; mesmo padrão dos sweeps já existentes).

> A numeração original foi mantida nos itens riscados para preservar histórico; a ordem de ataque
> efetiva hoje é: leituras de dashboard (§2) → purga/time-out (Infra §3).

> Nota de honestidade: nada aqui é pré-requisito do que já está entregue. O MVP REST+hub do Host
> está de pé e documentado; o listado acima é a segunda camada.

---

## 4. Implementação — `KeyRotationSweeper` (rotação de chave RF13)

> **Entregue em 01/09.** Fecha a lacuna que este mesmo doc listava: a Infra validava a chave
> (`AgentAuthenticator`) e persistia hash novo (`DeviceRepository.RotateKeyAsync`), e o agente já
> aceitava `RotateKey` — mas **nada disparava**. Era job de fundo, não endpoint.

### Arquivos envolvidos

| Arquivo | Mudança |
|---|---|
| `src/Host/informE.Infrastructure/BackgroundJobs/KeyRotationSweeper.cs` | **novo** — o `BackgroundService` que orquestra a rotação |
| `src/Host/informE.Infrastructure/BackgroundJobs/MonitoringOptions.cs` | 2 configs novas: `KeyRotationIntervalMinutes` (60) e `KeyMaxAgeDays` (30) |
| `src/Host/informE.Infrastructure/DependencyInjection.cs` | `services.AddHostedService<KeyRotationSweeper>()` |
| `src/Host/informE.Server/appsettings.json` | Seção `Monitoring` explícita (valores = defaults, só para o knob ficar visível/testável) |

**O que NÃO mudou:** nada na Application (não há `KeyRotationUseCase` de propósito — rotação é
orquestração de infraestrutura, mesmo padrão do `DeviceOfflineSweeper`/`ExpiredSessionSweeper`, que
também não passam por use case). Nenhuma rota nova no Server.

### Funcionalidades

- Roda num `PeriodicTimer` de `KeyRotationIntervalMinutes` (padrão 1 h), varrendo uma vez no boot
  e depois a cada tick — mesmo `do/while` do `DeviceOfflineSweeper`.
- Seleciona `Device`s com `KeyRotatedAt < agora − KeyMaxAgeDays` (campo nasce preenchido no
  construtor, então pega tanto nunca-rotacionado quanto vencido).
- Para cada vencido **online** (resolvido no `EndpointConnectionRegistry`):
  1. gera chave nova (32 bytes → Base64, mesmo RNG do `EnrollDeviceUseCase`);
  2. persiste **hash** novo via `Device.UpdateAgentHashKey` (domínio, toca `KeyRotatedAt`);
  3. empurra o **texto claro** pela conexão SignalR já autenticada (`IAgentClient.RotateKey` →
     agente grava com DPAPI e usa na próxima conexão).
- **Device offline é adiado, não rotacionado:** gravar o hash novo sem o agente receber a chave
  travaria a máquina até re-enroll. O sweeper tenta de novo no próximo ciclo.
- Falha de push não mata o resto do lote (`try/catch` por device) nem derruba o job (`catch`
  externo para banco fora do ar).

> Limitação assumida: o contrato `RotateKey` não tem ack. Se o push falhar no meio, o Host fica
> com o hash novo e o agente com a chave velha (lockout na próxima conexão, resolvido por
> re-enroll). É o preço de só-rotacionar-online; a janela é de milissegundos.

### Como testar (devs)

Pré-requisito: Server + Postgres de pé (`.start-db` / `start-db.ps1`) e ao menos 1 agente
registrado e online (enroll + hub conectado).

1. **Forçar rotação imediata** — em `src/Host/informE.Server/appsettings.json`, seção `Monitoring`:
   `"KeyMaxAgeDays": 0` e `"KeyRotationIntervalMinutes": 1` (roda no boot e a cada minuto).
2. Subir o Server e ver o log de boot:
   `Rotação de chave ativa: a cada 1 min, rotacionando chaves com mais de 0 dias.`
3. Nos logs do sweeper, esperar:
   `Chave rotacionada para <hostname>.` (por device online) — os **offline** exibem
   `rotacao adiada` e não gera `AgentKeyHash` novo.
4. **Conferir no banco** que `AgentKeyHash` mudou e `KeyRotatedAt` foi atualizado:
   ```sql
   select id, hostname, "agent_key_hash", "key_rotated_at" from devices;
   ```
5. **Conferir no agente** que ele recebeu (`Chave rotacionada pelo Host.` no log do Worker) e que
   **reconecta sem re-enroll** — derrube e suba o agente; ele volta `Online` com a chave nova
   (prova de que o hash persistido bate com a chave guardada pelo agente).
6. **Regressão:** `dotnet test informE.Host.slnx` (a rotação não toca em use case nem rota; nada
   deve quebrar).
7. Ao terminar, **reverter** `KeyMaxAgeDays` para 30 e `KeyRotationIntervalMinutes` para 60.

> ⚠️ Com `KeyMaxAgeDays: 0`, **todo** device entra na fila — incluindo os offline, que serão
> adiados, não rotacionados. Depois do teste, restaure a config para a rotina diária não trocar
> chaves em excesso.