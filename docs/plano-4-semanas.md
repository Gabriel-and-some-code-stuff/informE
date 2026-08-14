# Plano de 4 Semanas — informE

> Referencia: 14/08/2026. 5h por pessoa por semana fora da escola.
> Backend: Gabriel, Faggian, Augusto, Pedro (20h/semana coletivas)
> Frontend: Eduardo, Bruna (10h/semana coletivas)

---

## Semana 1 — ate 22/08 | Domain + Application prontos

**Meta:** Domain fechado, todos os use cases implementados com testes.

### Backend (20h)

Cada pessoa trabalha em uma branch separada e abre PR ao terminar.

| Pessoa | Branch | Tarefa | Horas |
|--------|--------|--------|-------|
| **Gabriel** | `feat/domain-methods` | Implementar metodos que faltam no Domain: `Session.Revoke()`, `Session.IsExpired()`, `EnrollmentToken.Redeem()`, `Device.MarkOnline()`, `Device.MarkOffline()`. Desbloquear os TODOs nos testes correspondentes. | 5h |
| **Faggian** | `feat/login-use-case` | `Models/LoginRequest.cs`, `Models/LoginResponse.cs`, `UseCases/LoginUseCase.cs`. Testes com NSubstitute mockando IUserRepository, IPasswordHasher, IJwtTokenService. | 5h |
| **Augusto** | `feat/enroll-use-case` | `Models/EnrollDeviceRequest.cs`, `Models/EnrollDeviceResponse.cs`, `UseCases/EnrollDeviceUseCase.cs`. Testes mockando IEnrollmentTokenRepository, IDeviceRepository. | 5h |
| **Pedro** | `feat/register-use-case` | `Models/RegisterUserRequest.cs`, `Models/RegisterUserResponse.cs`, `UseCases/RegisterUserUseCase.cs`. Tambem criar `Application/Exceptions/` com `UnauthorizedException` e `InvalidTokenException`. | 5h |

**Dependencias entre branches:**
- Faggian e Augusto podem comecar imediatamente — interfaces ja existem
- Gabriel deve abrir PR cedo: Faggian e Augusto precisam de `Redeem()` e `MarkOnline()` para os testes ficarem completos
- Pedro pode comecar em paralelo total

**Como instalar NSubstitute antes de comecar:**
```bash
dotnet add tests/informE.Application.Tests package NSubstitute
```

### Frontend (10h)

Nao depende do backend ainda — trabalhar com dados fake.

| Pessoa | Branch | Tarefa |
|--------|--------|--------|
| **Eduardo** | `feat/blazor-setup` | Setup do projeto Blazor (ja existe `informE.UI`): estrutura de paginas, roteamento, layout base com sidebar e topbar. |
| **Bruna** | `feat/blazor-login` | Pagina de login com formulario. Componente de loading. Dados fake por enquanto — so a UI. |

---

## Semana 2 — ate 29/08 | Infrastructure pronta

**Meta:** repositorios implementados, Argon2 e JWT funcionando, banco conectado de verdade.

### Backend (20h)

| Pessoa | Branch | Tarefa | Horas |
|--------|--------|--------|-------|
| **Gabriel** | `feat/user-repository` | Implementar `UserRepository`: `GetByEmailAsync`, `GetByIdAsync`, `AddAsync`, `AddSessionAsync`, `GetActiveSessionsAsync`, `RevokeSessionAsync`. EF Core puro — sem query manual, so LINQ. | 5h |
| **Faggian** | `feat/device-repository` | Implementar `DeviceRepository` e `EnrollmentTokenRepository`. Metodos definidos nas interfaces — so traduzir para EF. | 5h |
| **Augusto** | `feat/auth-services` | Implementar `PasswordHasher` (Argon2id via `Konscious.Security.Cryptography.Argon2`) e `JwtTokenService` (assinar e gerar tokens com `System.IdentityModel.Tokens.Jwt`). Esses dois sao criticos — qualquer duvida, resolver antes de tudo. | 5h |
| **Pedro** | `feat/agent-stub` | Console App stub do agent: conecta no AgentHub via SignalR, manda `TelemetryDto` fake a cada 5s, escuta por `CommandDto`. Nao precisa de MAUI — so provar que a comunicacao funciona. Adicionar projeto em `src/Agent/informE.AgentStub/`. | 5h |

**Pacotes necessarios:**
```bash
# Augusto — PasswordHasher
dotnet add src/Host/informE.Infrastructure package Konscious.Security.Cryptography.Argon2

# Pedro — Agent stub
dotnet new console -o src/Agent/informE.AgentStub
dotnet add src/Agent/informE.AgentStub package Microsoft.AspNetCore.SignalR.Client
```

**Por que o agent stub entra aqui:**
Sem algum client conectando no hub, a semana 3 inteira (SignalR) nao tem como ser testada. Pedro monta o stub agora para que na semana 3 o hub possa ser validado de ponta a ponta.

### Frontend (10h)

| Pessoa | Branch | Tarefa |
|--------|--------|--------|
| **Eduardo** | `feat/blazor-dashboard` | Pagina de dashboard: lista de devices com status (Online/Offline/Unknown), metricas resumidas. Dados ainda hardcoded — componentes prontos para receber dados reais. |
| **Bruna** | `feat/blazor-device-detail` | Pagina de detalhe do device: graficos de CPU/RAM/Disco nos ultimos 7 dias (biblioteca de graficos recomendada: `Radzen.Blazor`). Dados fake por enquanto. |

---

## Semana 3 — ate 06/09 | Server + Blazor integrados

**Meta:** sistema rodando de ponta a ponta — login real, device real, telemetria real no dashboard.

### Backend (20h)

| Pessoa | Branch | Tarefa | Horas |
|--------|--------|--------|-------|
| **Gabriel** | `feat/server-auth-endpoints` | Endpoints no Server: `POST /auth/login`, `POST /auth/register`, `POST /auth/refresh`. Configurar JWT Bearer no `Program.cs`. Swagger funcionando. | 5h |
| **Faggian** | `feat/agent-hub` | Implementar `AgentHub`: agent autentica com chave rotativa, `OnConnectedAsync` chama `Device.MarkOnline()` + persiste, `OnDisconnectedAsync` chama `MarkOffline()`. Recebe `TelemetryDto` e repassa para `IDashboardNotifier`. | 5h |
| **Augusto** | `feat/dashboard-hub` | Implementar `DashboardHub`: operador autentica com JWT, recebe broadcasts de telemetria e status. Implementar `IEndpointConnectionRegistry` em memoria (dicionario thread-safe). Implementar `IDashboardNotifier`. | 5h |
| **Pedro** | `feat/server-device-endpoints` | Endpoints: `POST /agent/enroll`, `GET /devices`, `GET /devices/{id}`, `GET /devices/{id}/metrics`. Testar com o agent stub da semana 2. | 5h |

**Dependencias criticas:**
- Gabriel deve configurar JWT Bearer antes de Faggian e Augusto finalizarem os hubs — os hubs precisam do middleware de auth no pipeline
- Augusto depende de Faggian ter o AgentHub estruturado para saber o que o DashboardHub precisa receber
- Pedro pode trabalhar em paralelo total

### Frontend (10h)

| Pessoa | Branch | Tarefa |
|--------|--------|--------|
| **Eduardo** | `feat/blazor-auth-integration` | Integrar login real: chamar `POST /auth/login`, guardar JWT no `localStorage`, configurar `HttpClient` com Bearer token. |
| **Bruna** | `feat/blazor-data-integration` | Trocar dados fake por chamadas reais: `GET /devices`, `GET /devices/{id}/metrics`. Conectar no DashboardHub via SignalR client para atualizacao em tempo real. |

**Pacote SignalR para Blazor:**
```bash
dotnet add src/Host/informE.UI package Microsoft.AspNetCore.SignalR.Client
```

---

## Semana 4 — ate onde der | Agent real + execucao remota

**Meta:** agent MAUI conectando, execucao remota funcionando.

Esta semana e buffer + bonus. Prioridade em ordem:

| Prioridade | Pessoa(s) | Tarefa |
|-----------|-----------|--------|
| 1 | Pedro | Migrar logica do agent stub para o projeto MAUI (`informE.Desktop`). A logica do SignalR client ja estara pronta. |
| 2 | Gabriel | `POST /tasks` — criar MachineTask com script e lista de devices alvo. |
| 3 | Faggian | `DispatchTaskUseCase`: busca connections no IEndpointConnectionRegistry, manda `CommandDto` pelo AgentHub. |
| 4 | Augusto | Handler de resultado: agent manda `CommandResultDto` de volta, persiste `TaskExecutionLog`. |
| 5 | Eduardo + Bruna | Pagina de tasks no Blazor: criar task, ver status de execucao por device. |

---

## Visao geral — dependencias entre times

```
Semana 1: Backend (Domain + UseCases) ←→ Frontend (UI fake, sem dependencia)
Semana 2: Backend (Infrastructure + Stub) ←→ Frontend (componentes prontos)
Semana 3: INTEGRACAO — Frontend consome endpoints e hubs reais
Semana 4: Feature extra — todos em paralelo
```

O frontend pode trabalhar as duas primeiras semanas completamente independente. A integracao real acontece na semana 3 — por isso os componentes precisam estar prontos com dados fake antes disso.

---

## Regras de trabalho

- Toda tarefa em uma branch separada, nunca commitar direto no master
- PR aberta ao terminar, mesmo que incompleta — feedback antes de mergear
- Se travar mais de 1h num problema: pedir ajuda no grupo antes de perder a semana inteira
- Testes unitarios entram na mesma PR que o codigo — nao na "proxima vez"
