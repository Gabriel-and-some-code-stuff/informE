# CLAUDE.md — Guia de contexto para assistentes de IA

Este arquivo descreve o projeto **informE** para que assistentes de IA
(Claude, Copilot, etc.) entendam a arquitetura, as decisões tomadas e
as convenções do código antes de sugerir qualquer alteração.

---

## O que é o informE

RMM (Remote Monitoring & Management) **on-premise** para laboratórios de
escola (Etec) e PMEs. Tudo roda na rede do cliente, sem nuvem. É um TCC
com prazo de entrega em 15/10/2026.

O técnico usa o **Desktop** para monitorar máquinas, executar comandos
remotos e ver telemetria ao vivo. Cada máquina monitorada roda o
**Agent** (Windows Service). O **Server** é o intermediário.

---

## Arquitetura

```
[informE.Desktop]  <--- REST + SignalR --->  [informE.Server]  <--- SignalR --->  [informE.Agent]
  (MAUI Blazor,          DashboardHub           |        |          AgentHub       (Windows Service)
   técnico usa)                               EF Core  Npgsql
                                                  ↓
                                          [PostgreSQL — Docker]
```

**Duas soluções, três executáveis:**

| Solução | Projetos |
|---|---|
| `informE.Host.slnx` | Server + Desktop + Domain + Application + Infrastructure + UI + Contracts |
| `informE.Agent.slnx` | Agent.Worker + Agent.Core + Agent.Application + Agent.Infrastructure + Contracts |

`informE.Contracts` é referenciado pelas DUAS soluções (mesmo `.csproj`, duas `.sln`).

---

## Onion Architecture — regra de ouro

As dependências apontam **sempre para dentro**:

```
Contracts (zero deps)
  ↑
Domain (zero deps)
  ↑
Application (depende de Domain + Contracts)
  ↑
Infrastructure (depende de Application + Domain + Contracts)
  ↑
Server / Desktop / Agent.Worker (composition root — depende de tudo)
```

**Nunca** referencie uma camada externa de dentro de uma interna.
O CI (`.github/workflows/ai-review.yml`) revisa PRs buscando exatamente isso.

---

## Decisões travadas (não discutir sem motivo forte)

| Decisão | Motivo |
|---|---|
| **UI = MAUI Blazor Hybrid** | Cross-platform, componentes Blazor reutilizáveis na RCL `informE.UI` |
| **Sem ASP.NET Core Identity** | Usa JWT + Argon2id + tabela `Users` própria; menos magia, mais controle |
| **Auth do agente = enrollment token → chave rotativa (DPAPI)** | RF05–08; token de uso único, chave persistida com DPAPI no disco do agente |
| **IDs = Guid (uuid)** | Evita enumeração sequencial; sem surrogate INT |
| **EF Code-First** | Nunca editar tabelas diretamente no banco; sempre entidade → migration |
| **Datas = DateTimeOffset.UtcNow** | Npgsql recusa DateTimeOffset com offset ≠ 0 em `timestamptz` |
| **Sem broker externo** | SignalR resolve pub/sub e reconexão; EF resolve persistência |
| **UI nunca manda script** | A UI escolhe uma ação do catálogo (`MachineActionKind`); o Server resolve o script (RF14) |

---

## Estrutura de pastas

```
informE/
├── docker-compose.yml          # Postgres + Server (imagens Debian)
├── docker-compose2.yml         # Postgres + Server (imagens Alpine, ~465 MB)
├── Directory.Build.props       # Nullable, TreatWarningsAsErrors, isolamento obj/bin no container
├── global.json                 # Trava SDK .NET 10.0.100
├── informE.Host.slnx
├── informE.Agent.slnx
├── README.md
├── docs/                       # Toda a documentação
│   ├── COMO-RODAR.md           # Guia de usuário (não-técnico + técnico)
│   ├── ARCHITECTURE.md         # Arquitetura completa + Sprint 1
│   ├── situacao-atual.md       # Status honesto do projeto
│   └── ...
├── ps1/                        # Scripts PowerShell
│   ├── informe.ps1             # ← script principal: sobe tudo
│   ├── novo-agente.ps1         # gera token + publica .exe do agente
│   ├── run-agents.ps1          # sobe N agentes simulados na mesma máquina
│   └── start-local.ps1        # sobe Postgres sem Docker (pg_ctl)
└── src/
    ├── Shared/informE.Contracts/
    ├── Host/
    │   ├── informE.Domain/
    │   ├── informE.Application/
    │   ├── informE.Infrastructure/
    │   ├── informE.Server/
    │   ├── informE.UI/         # Razor Class Library (componentes Blazor compartilhados)
    │   └── informE.Desktop/    # MAUI Blazor Hybrid
    └── Agent/
        ├── informE.Agent.Core/
        ├── informE.Agent.Application/
        ├── informE.Agent.Infrastructure/
        └── informE.Agent.Worker/
```

---

## Como rodar (dev)

### Via scripts (recomendado para demo)

```powershell
# Sobe Postgres + Server + Desktop + registra esta máquina como agente
powershell -ExecutionPolicy Bypass -File ps1\informe.ps1

# Registrar outra máquina (gera token + publica .exe)
powershell -ExecutionPolicy Bypass -File ps1\novo-agente.ps1 -Publicar

# Simular N agentes na mesma máquina (dev/demo)
powershell -ExecutionPolicy Bypass -File ps1\run-agents.ps1 -Count 3
```

### Via Docker Compose

```bash
# Sobe Postgres + Server com hot reload (dotnet watch)
docker compose up

# Variante Alpine (~465 MB total vs ~1,35 GB)
docker compose -f docker-compose2.yml up  # (opção principal / padrão)
```

API: `http://localhost:5020` | Scalar: `http://localhost:5020/scalar/v1`

### Via dotnet run (manual)

```bash
docker compose up -d                          # só o banco
dotnet run --project src/Host/informE.Server  # servidor (migra e semeia sozinho)
dotnet run --project src/Host/informE.Desktop # desktop
```

Login de dev: `admin@cps.sp.gov.br` / `informe123`

---

## Banco de dados

- **Nunca edite tabelas diretamente.** Altere a entidade em `informE.Domain`,
  gere a migration e aplique:
  ```bash
  dotnet ef migrations add NomeDaMigration -p src/Host/informE.Infrastructure -s src/Host/informE.Server
  dotnet ef database update -p src/Host/informE.Infrastructure -s src/Host/informE.Server
  ```
- O `Server` aplica migrations e semeia o banco automaticamente no boot
  (`DatabaseBootstrapper`).
- Connection string: `appsettings.json` → `ConnectionStrings:Postgres`.
  Dentro do Docker Compose, sobrescrita por variável de ambiente
  (`ConnectionStrings__Postgres` com `Host=postgres`).

---

## Convenções de código

```csharp
// Nullable: obrigatório em todos os projetos (Directory.Build.props)
public string? Message { get; set; }          // pode ser null
public string Message { get; set; } = string.Empty;  // não-nullable, começa vazio
public string Message { get; set; } = null!;  // não-nullable, preenchido externamente

// Datas: SEMPRE UTC
var agora = DateTimeOffset.UtcNow;  // nunca DateTime.Now ou DateTimeOffset.Now

// IDs: sempre Guid
public Guid Id { get; set; }
```

`TreatWarningsAsErrors=true` — warnings quebram o build.

---

## Scripts PowerShell — atenção ao $PSScriptRoot

Os scripts ficam em `/ps1/`, não na raiz. Todos que usam `$raiz` fazem:

```powershell
$raiz = Split-Path $PSScriptRoot -Parent  # aponta para a raiz do repo
```

`start-local.ps1` é chamado por dot-source de dentro do `informe.ps1`:
```powershell
. (Join-Path $PSScriptRoot 'start-local.ps1')  # mesma pasta /ps1/
```

---

## Docker Compose — detalhes técnicos

- **Perfil `docker`** no `launchSettings.json`: HTTP puro (`http://0.0.0.0:5020`),
  `launchBrowser: false`. Usado com `--launch-profile docker` no compose.
- **`Directory.Build.props`**: quando `DOTNET_RUNNING_IN_CONTAINER=true`
  (injetado pela imagem oficial), redireciona `obj/` e `bin/` para `/tmp`
  e exclui `obj/**;bin/**` dos globs — evita CS0579 (atributo duplicado)
  causado pelos artefatos do Visual Studio no bind mount.
- **Alpine + DNS**: `docker-compose2.yml` usa `dns: ["8.8.8.8", "1.1.1.1"]`
  para contornar o resolver musl que não alcança `api.nuget.org` no Docker Desktop.

---

## O que está feito vs o que falta

### ✅ Feito (backend completo)
- Autenticação JWT + Argon2id (login, refresh com rotação, logout, lockout por papel)
- CRUD de usuários (listar, criar, editar, ativar/desativar, sessões, revogar)
- Enrollment de agentes (token de uso único, re-enroll por MAC)
- Execução remota em paralelo (catálogo de 7 ações, N máquinas/grupos)
- Telemetria ao vivo (SignalR)
- 160 testes verdes

### ⚠️ Pendente (ver `docs/situacao-atual.md`)
- Paginação em `/devices` e `/users`
- `SignalRDashboardNotifier` usa `Clients.All` (sem filtro por grupo)
- Cancelamento de tarefa não interrompe agente que já recebeu o comando
- `DeviceInfo` e `Software` nunca são populados (inventário HW/SW)
- Rotas de dashboard/alerta/métrica sem consumidor
