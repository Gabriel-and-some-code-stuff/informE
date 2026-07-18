# informE

Monitoramento e gerenciamento remoto de máquinas (RMM) para labs de escola e PMEs.
Roda 100% na rede do cliente, sem nuvem. TCC.

> Arquitetura completa, modelo de domínio e o plano da Sprint 1: **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**.
> Análise do banco de dados e decisões de port: **[docs/ANALISE-BD.md](docs/ANALISE-BD.md)**.

## Estrutura

Duas soluções, três executáveis:

- **`informE.Host.slnx`** — o lado do gerenciamento (roda no servidor on-prem do cliente):
  - `informE.Server` — ASP.NET Core: API REST (JWT) + `AgentHub` + `DashboardHub` (SignalR) + composition root.
  - `informE.Desktop` — MAUI Blazor Hybrid: o cliente instalado que o técnico usa.
  - `informE.Domain` / `informE.Application` / `informE.Infrastructure` — camadas Onion.
  - `informE.UI` — componentes Blazor compartilhados.
- **`informE.Agent.slnx`** — o agente (Windows Service) que roda em cada máquina monitorada.
- **`informE.Contracts`** — DTOs e contratos de hub, compartilhado pelas duas soluções.

## Como tudo se conecta

```
[informE.Desktop]  <--- REST + SignalR --->  [informE.Server]  <--- SignalR --->  [informE.Agent]
  (MAUI, técnico)      (DashboardHub)          |      |            (AgentHub)      (Windows Service,
                                                |      |                            máquina monitorada)
                                          EF Core |    | Npgsql
                                                  v    v
                                          [ PostgreSQL — container Docker ]
```

- **Desktop ↔ Server**: o técnico loga (JWT), consulta dados via REST e recebe
  telemetria/alertas ao vivo pelo `DashboardHub` (SignalR).
- **Agent ↔ Server**: o agente faz enrollment via REST, depois mantém uma
  conexão persistente no `AgentHub` (SignalR) — sobe telemetria/alertas, recebe
  comandos.
- **Server ↔ Postgres**: só o `informE.Server` fala com o banco, via
  `informE.Infrastructure` (EF Core). Nem o Desktop nem o Agent tocam no banco
  diretamente — sempre passam pelo Server.

## Onde as coisas moram

| O quê | Onde |
|---|---|
| **Banco de dados (dados reais)** | Container Docker `informe-postgres`, volume nomeado `informe_pgdata` — gerenciado pelo Docker (dentro do WSL2), **não é uma pasta do projeto**. Sobrevive a `docker compose down`; some só com `docker compose down -v`. |
| **Definição das tabelas (código-fonte)** | `src/Host/informE.Domain/Entities/*.cs` (as entidades C#) + `src/Host/informE.Infrastructure/Persistence/Configurations/*.cs` (como cada entidade vira tabela — Fluent API). |
| **Migrations (histórico versionado do schema)** | `src/Host/informE.Infrastructure/Persistence/Migrations/`. Cada mudança nas entidades gera uma migration nova aqui; é assim que o banco evolui de forma rastreável. |
| **Config de conexão** | `src/Host/informE.Server/appsettings.json` → chave `ConnectionStrings:Postgres`. |
| **Como o Docker sobe o banco** | `docker-compose.yml` na raiz — imagem `postgres:16`, usuário/senha/porta, healthcheck. |
| **Dump de referência do desenho original (BD2)** | `docs/db/estrutura_informe_db.sql` — histórico, não é mais a fonte da verdade (o EF Code-First é). |

**O banco nasce do código, não o contrário** (EF Code-First): você escreve/edita
uma entidade em `informE.Domain`, roda `dotnet ef migrations add`, o EF gera o
SQL de uma migration nova, e `dotnet ef database update` aplica isso no
Postgres. Isso significa: **nunca edite tabelas direto no banco** — sempre mude
a entidade C# e gere uma migration.

## Rodar em dev

```bash
docker compose up -d                 # sobe o Postgres (container informe-postgres)
dotnet ef database update -p src/Host/informE.Infrastructure -s src/Host/informE.Server
dotnet run --project src/Host/informE.Server        # o servidor
dotnet run --project src/Host/informE.Desktop       # o cliente desktop (Windows)
dotnet run --project src/Agent/informE.Agent.Worker # o agente (numa VM Windows)
```

Ou rode `setup-dev.ps1` (raiz do repo) — instala tudo sem admin (.NET 10, MAUI,
gh CLI) e já sobe o Postgres + aplica as migrations sozinho.

Requisitos: **.NET 10 SDK**, workload **maui-windows** (`dotnet workload restore informE.Host.slnx`), Docker Desktop.

> ⚠️ Se `dotnet ef database update` falhar com erro de senha, confira se não há
> outro Postgres (nativo, instalado fora do Docker) ocupando a porta 5432 —
> ver `docs/ANALISE-BD.md` §0.

## Ver as tabelas no banco

```bash
docker exec -it informe-postgres psql -U informe -d informe -c "\dt"
```
