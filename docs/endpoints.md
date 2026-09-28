# Mapeamento de Endpoints — informE.Server

> Portas padrão: **HTTP** `5020` · **HTTPS** `5021`  
> Autenticação padrão: Bearer JWT (access token de 15 min).  
> Endpoints marcados como **Anônimo** não exigem token.

---

## HTTP / HTTPS

### Raiz

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| `GET` | `/` | Anônimo | Health-check mínimo — retorna `"informE.Server online"`. |

---

### Autenticação — `/auth`

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| `POST` | `/auth/login` | Anônimo | Autentica e devolve access token + refresh token. |
| `POST` | `/auth/refresh` | Anônimo | Troca refresh token por novo par (token rotacionado). |
| `POST` | `/auth/logout` | JWT | Revoga o refresh token da sessão atual. |
| `POST` | `/auth/forgot-password` | Anônimo | Envia link de redefinição para o e-mail informado. |
| `POST` | `/auth/reset-password` | Anônimo | Define nova senha a partir do token recebido por e-mail. |

---

### Usuários — `/users`

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| `GET` | `/users/me` | JWT | Dados do próprio perfil. |
| `GET` | `/users/me/sessions` | JWT | Lista sessões ativas do usuário autenticado. |
| `DELETE` | `/users/me/sessions/{sessionId}` | JWT | Encerra uma sessão específica (própria ou de terceiros, conforme papel). |
| `GET` | `/users/` | Admin/SuperAdmin | Lista usuários com filtros `papel`, `ativo`, `busca`. |
| `GET` | `/users/{id}` | Admin/SuperAdmin | Detalhe de um usuário. |
| `POST` | `/users/` | Admin/SuperAdmin | Cria um usuário (SuperAdmin cria qualquer papel; Admin só cria Viewer). |
| `PATCH` | `/users/{id}` | JWT | Atualiza nome e/ou e-mail. |
| `PATCH` | `/users/{id}/role` | SuperAdmin | Promove ou rebaixa o papel (revoga sessões do alvo). |
| `PATCH` | `/users/{id}/active` | Admin/SuperAdmin | Ativa ou desativa a conta (desativar encerra todas as sessões). |

---

### Grupos — `/groups`

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| `GET` | `/groups/` | JWT | Lista laboratórios visíveis (Viewer vê apenas os próprios; Admin+ vê todos). |
| `POST` | `/groups/` | Admin/SuperAdmin | Cria um laboratório (quem cria vira dono). |

---

### Equipamentos — `/devices`

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| `GET` | `/devices/` | JWT | Lista equipamentos com filtros `grupoId`, `status`, `busca`. Viewer vê apenas os laboratórios próprios. |
| `GET` | `/devices/{id}` | JWT | Detalhe de um equipamento + hardware (se houver coleta). |

---

### Execuções — `/actions` e `/tasks`

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| `GET` | `/actions` | JWT | Catálogo de ações disponíveis (alimenta o dropdown da tela Nova Execução). |
| `POST` | `/tasks/` | JWT | Dispara uma ação em N dispositivos e/ou grupos. |
| `GET` | `/tasks/` | JWT | Execuções recentes (uma linha por máquina). Query param `limite` conta tarefas. |
| `GET` | `/tasks/{id}` | JWT | Detalhe de uma execução: status da tarefa + uma linha por máquina. |
| `POST` | `/tasks/{id}/cancel` | JWT | Cancela uma execução pendente, enfileirada ou em andamento. |

---

### Alertas — `/alerts`

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| `GET` | `/alerts/` | JWT | Alertas do período com histórico diário, contagem por categoria e lista de recentes. Params: `dias` (1–90), `grupoId`, `categoria`, `recentes`. |

---

### Agente — `/agent` e `/admin`

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| `POST` | `/agent/enroll` | Anônimo | Registra a máquina e devolve a `agentKey` permanente (usa `enrollmentToken`). |
| `POST` | `/admin/enrollment-tokens` | Admin/SuperAdmin | Gera token de uso único (2 h) para registrar uma nova máquina. |
| `POST` | `/admin/seed` | SuperAdmin | **Apenas Development.** Repõe a massa de dados sem derrubar o container. |

---

### OpenAPI / Scalar

| Rota | Disponível | Descrição |
|------|-----------|-----------|
| `GET /openapi/v1.json` | Apenas Development | Spec OpenAPI gerada automaticamente. |
| `GET /scalar/v1` | Apenas Development | UI interativa para testar os endpoints. |

---

## WebSocket / SignalR (WSS)

Os dois hubs são mapeados via SignalR. Em produção o transporte é sempre **WSS** (`wss://`). Em desenvolvimento, com o agente em máquina remota, aceita **WS** na porta 5020 (`ws://`) para evitar o problema de redirecionamento de certificado.

### AgentHub — `/hubs/agent`

Canal bidirecional entre o **Server** e cada **agente instalado nas máquinas**.  
A autenticação é feita por `agentKey` + `deviceId` na query string do handshake (não usa JWT).

| Direção | Método / Evento | Descrição |
|---------|----------------|-----------|
| Agente → Server | `ReportTelemetry(TelemetryDto)` | Envia heartbeat periódico com CPU, RAM, disco e uptime. |
| Agente → Server | `ReportCommandResult(CommandResultDto)` | Devolve stdout/stderr + duração de um comando executado. |
| Server → Agente | `RunCommand(CommandDto)` | Envia um comando para a máquina executar. |
| Server → Agente | `RotateKey(string newKey)` | Envia nova chave rotativa; o agente persiste com DPAPI. |

**Query string do handshake:** `?deviceId={guid}&agentKey={key}`

---

### DashboardHub — `/hubs/dashboard`

Canal de **saída** do servidor para os operadores (UI).  
Autenticação: **JWT** lido da query string (`?access_token=...`), pois WebSocket não envia cabeçalho Authorization.  
**Não há métodos de entrada** — o dashboard só escuta; todas as consultas são feitas por REST.

| Direção | Evento | Descrição |
|---------|--------|-----------|
| Server → Cliente | `EndpointStatusChanged(deviceId, status, health)` | Notifica mudança de conexão ou saúde de um equipamento. |
| Server → Cliente | `TelemetryUpdated(TelemetryDto)` | Empurra métricas em tempo real (CPU/RAM/disco/uptime). |
| Server → Cliente | `AlertRaised(AlertDto)` | Notifica novo alerta gerado. |
| Server → Cliente | `TaskProgress(taskId, status)` | Atualiza o status global de uma tarefa quando ela encerra. |
| Server → Cliente | `ExecutionLogUpdated(logId, taskId, status, durationMs, output)` | Atualiza o log por máquina em tempo real durante a execução. |

**Query string do handshake:** `?access_token={jwt}`

---

## Resumo

| Tipo | Quantidade |
|------|-----------|
| Endpoints HTTP (incluindo dev-only) | 26 |
| Hubs SignalR (WSS) | 2 |
| WebSocket puro (sem SignalR) | **0** |

> **Não existe nenhum endpoint WebSocket puro.** Todo o tráfego em tempo real passa pelos hubs SignalR (`/hubs/agent` e `/hubs/dashboard`), que por padrão negocia WebSocket mas pode recuar para Server-Sent Events ou Long Polling dependendo da infraestrutura.
