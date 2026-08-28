# Host — Status das 3 camadas (Server / Application / Infrastructure)

> Atualizado em **28/08/2026**. Fontes: `docs/api-server.md`, `docs/ARCHITECTURE.md`,
> leitura direta de `src/Host/` e do DI (`DependencyInjection.cs`). Branch de trabalho: `prs-acumuladas`.

## Visão geral

| Camada | Estado | O que falta (resumo) |
|---|---|---|
| **informE.Server** | **10 rotas** REST + 2 hubs funcionando | **~24 rotas ausentes** (MVP: refresh/logout/sessões/users/tasks-detalhe; deferidas: dashboard/grupos) |
| **informE.Application** | 12 use cases + ports completos | Leitura de dashboard, orquestração de rotação de chave, kick em tempo real |
| **informE.Infrastructure** | Persistência + realtime + security + sweeps fechados | Sweeper de rotação de chave, purga de auditoria, time-out de tarefa pendente |

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

**BackgroundJobs** — varreduras periódicas (5 min por padrão, `MonitoringOptions`):
- `DeviceOfflineSweeper` — RN03: offline após `OfflineThresholdMinutes` (90 min padrão, múltiplo do snapshot de 30 min);
- `ExpiredSessionSweeper` — sessões inativas.

**E-mail**: `SmtpEmailSender` + `SmtpOptions` (base do reset de senha).

**Seeding**: `DatabaseBootstrapper` (migrate no boot) + `SeedData` (só Development, determinístico,
105 devices + 6 usuários com `informe123`).

### ❌ O que falta

1. **Driver de rotação de chave.** A Infra valida (`AgentAuthenticator`) e persiste hash
   (`DeviceRepository.RotateKeyAsync`), mas **nada rotaciona**: não existe `HostedService` tipo
   `KeyRotationSweeper`, nem config de intervalo. Mesmo com o use case da §2, alguém precisa chamá-lo.
2. **Purga de audit log.** `ARCHITECTURE.md:542` marca "Purga automática: **ainda não**". Os
   sweeps existem para device offline e sessão expirada, mas nada de retenção/expurgo do
   `AuditLog` — cresce sem limite.
3. **Time-out de tarefa pendente.** Comando despachado com o agente offline deixa o
   `TaskExecutionLog` em `Pending` (o agente ainda não pede os pendentes na reconexão —
   `agente.md:239`). O contraponto no Host também não existe: **nenhum job marca como falha**
   a tarefa que nunca rodou. Sem fila de reconexão num lado e expiração no outro, o log fica
   `Pending` eterno.

---

## Prioridade sugerida (ordem de ataque)

1. **Application — leituras de dashboard** (desbloqueia a rota de dashboard do Server; é a lacuna
   com maior impacto visível).
2. **Server — `/auth/refresh`** (o refresh já é emitido e persistido; é o furo mais estranho da API).
3. **Rotação de chave** (use case na Application **+** sweeper na Infra, que são dois lados da
   mesma estória; o agente já aceita `RotateKey`).
4. **Server — rotas restantes** (`logout`, `set-user-active`, `change-role`, reset de senha):
   use cases já existem, é só rotear.
5. **Infra — purga de auditoria e time-out de tarefa pendente** (baixo risco, alto valor
   operacional; mesmo padrão dos sweeps já existentes).

> Nota de honestidade: nada aqui é pré-requisito do que já está entregue. O MVP REST+hub do Host
> está de pé e documentado; o listado acima é a segunda camada.