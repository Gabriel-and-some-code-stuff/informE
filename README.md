# informE — RMM on-premise

Monitoramento e gerenciamento remoto de máquinas (RMM) para labs de escola e PMEs.
Roda 100% na rede do cliente, sem nuvem. TCC.

> Arquitetura completa, modelo de domínio e o plano da Sprint 1: **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**.

## Estrutura

Duas soluções, três executáveis:

- **`informE.Host.slnx`** — o lado do gerenciamento (roda no servidor on-prem do cliente):
  - `informE.Server` — ASP.NET Core: API REST (JWT) + `AgentHub` + `DashboardHub` (SignalR) + composition root.
  - `informE.Desktop` — MAUI Blazor Hybrid: o cliente instalado que o técnico usa.
  - `informE.Domain` / `informE.Application` / `informE.Infrastructure` — camadas Onion.
  - `informE.UI` — componentes Blazor compartilhados.
- **`informE.Agent.slnx`** — o agente (Windows Service) que roda em cada máquina monitorada.
- **`informE.Contracts`** — DTOs e contratos de hub, compartilhado pelas duas soluções.

Dependências apontam **para dentro** (Onion): `Domain` ← `Application` ← `Infrastructure` ← executáveis.

## Rodar em dev

```bash
docker compose up -d                 # sobe o Postgres
dotnet ef database update -p src/Host/informE.Infrastructure -s src/Host/informE.Server
dotnet run --project src/Host/informE.Server        # o servidor
dotnet run --project src/Host/informE.Desktop       # o cliente desktop (Windows)
dotnet run --project src/Agent/informE.Agent.Worker # o agente (numa VM Windows)
```

Requisitos: **.NET 10 SDK**, workload **maui-windows** (`dotnet workload restore informE.Host.slnx`), Docker Desktop.
