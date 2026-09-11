# API do Server

> Endpoints REST + hubs SignalR. Atualizado em 28/08/2026.
> Base de desenvolvimento: `https://localhost:5021`

Para explorar interativamente, suba o Server em Development e abra
**`/scalar/v1`** no navegador — UI que lê `/openapi/v1.json` (gerado dos
próprios endpoints) e deixa testar cada rota clicando, com o `Authorize`
guardando o Bearer token entre chamadas. Substitui o curl imenso pra rodar
o agente (`/agent/enroll`, `/tasks`, etc.) — ver `docs/scalar.md`.

---

## Autenticação

Tudo exige `Authorization: Bearer <access_token>`, exceto `/auth/login`,
`/agent/enroll` e `/`.

```bash
curl -X POST https://localhost:5021/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@etec.sp.gov.br","password":"informe123"}'
```

Devolve `accessToken` (15 min), `refreshToken` (7 dias), `userId`, `username` e
`role`.

> ⚠️ **Não existe endpoint de refresh ainda.** O `refreshToken` é emitido e
> persistido, mas nada o consome — quando o access token vencer, é login de novo.
> É a lacuna mais visível da API hoje.

### IP e dispositivo vêm da requisição, não do corpo

O `LoginRequestDto` só tem e-mail e senha. O IP sai de `RemoteIpAddress` e o
rótulo do dispositivo do `User-Agent`. Se o cliente pudesse informar o próprio
IP, o log de auditoria e o painel de dispositivos viravam ficção.

---

## Endpoints

| Método | Rota | Quem pode | O quê |
|---|---|---|---|
| `POST` | `/auth/login` | anônimo | Autentica |
| `POST` | `/users` | Admin, SuperAdmin | Cria usuário |
| `GET` | `/devices` | autenticado | Lista equipamentos (+ resumo) |
| `GET` | `/devices/{id}` | autenticado | Um equipamento |
| `GET` | `/actions` | autenticado | Catálogo de ações (dropdown) |
| `POST` | `/tasks` | autenticado | Dispara ação em N máquinas/grupos |
| `GET` | `/tasks` | autenticado | Execuções recentes |
| `POST` | `/tasks/{id}/cancel` | autenticado | Cancela execução |
| `POST` | `/agent/enroll` | **anônimo** | Registra máquina |
| `POST` | `/admin/enrollment-tokens` | Admin, SuperAdmin | Emite token de registro |

### `GET /devices`

Filtros opcionais e combináveis: `?grupoId=<guid>`, `?status=Online|Offline|Unknown`,
`?busca=<texto>` (nome, IP ou SO — case-insensitive via `ILIKE`).

O `resumo` (`total`, `online`, `offline`, `comProblema`) é calculado sobre os
itens **filtrados**, não sobre o banco inteiro: os big numbers têm que acompanhar
o filtro que o usuário aplicou.

`uptimeSeconds` e `lastSeenAt` vêm `null` em máquina offline — a tela mostra `—`.

### `POST /tasks`

```json
{
  "action": "InformacoesDoSistema",
  "deviceIds": ["..."],
  "groupIds": null,
  "scheduledAt": null
}
```

`action` é o `kind` vindo de `/actions` — **nunca um script**. `scheduledAt: null`
significa "agora"; com data, é o toggle de agendamento da tela. `deviceIds` e
`groupIds` podem vir juntos: máquina presente nos dois não recebe o comando duas
vezes.

Devolve o `code` legível (`EX-1000`) além do `taskId`.

### `GET /tasks`

O grão é **por máquina**, não por tarefa: uma ação disparada em 20 devices vira
20 linhas — que é como a tela de Execuções mostra.

### `POST /users`

```json
{
  "username": "professor01",
  "email": "professor01@etec.sp.gov.br",
  "password": "senha-forte",
  "role": "Viewer"
}
```

`role` em texto (`"Viewer"`, `"Admin"` ou `"SuperAdmin"`) — 400 se não bater com
o enum. Quem pode criar quem é decidido pelo `CreateUserUseCase` a partir do
papel de quem está logado (claim do JWT), não pelo corpo da requisição:

- **SuperAdmin** cria qualquer papel, inclusive outro SuperAdmin;
- **Admin** cria **só** Viewer — não promove ninguém ao próprio nível;
- **Viewer** não cria ninguém (barrado no endpoint, `RequireRole` nem deixa
  chegar no use case).

E-mail duplicado devolve 409, não 400 — a requisição está bem formada, o
estado do banco que não permite.

### `POST /agent/enroll` — por que é anônimo

O agente ainda não tem credencial nenhuma; é justamente isso que ele vem buscar.
Quem autoriza é o `EnrollmentToken`, que:

- só um Admin/SuperAdmin logado consegue emitir;
- é de **uso único** (segunda tentativa → 400);
- expira em 2 horas.

A resposta traz `agentKey` em **texto claro uma única vez** — o agente guarda com
DPAPI e o servidor mantém só o hash Argon2id. Perdeu, precisa de enroll novo.

---

## Hubs SignalR

| Hub | Rota | Autenticação |
|---|---|---|
| `AgentHub` | `/hubs/agent` | Chave rotativa na query string do handshake |
| `DashboardHub` | `/hubs/dashboard` | JWT (`?access_token=`) |

**Por que o token vai na query string:** WebSocket no browser não permite header
`Authorization` no handshake. O padrão oficial é passar na query e reconstruir no
`OnMessageReceived` — restrito à rota do hub, para o token não vazar em log de
acesso de outras rotas (ver `AuthenticationSetup`).

O `AgentHub` **não** tem `[Authorize]` de propósito: o agente não usa JWT. O
filtro é o `OnConnectedAsync`, que valida a chave e aborta a conexão se não bater.

---

## Erros

Toda exceção de negócio vira `ProblemDetails` (RFC 7807) com a mensagem que a
própria exceção carrega. O stack fica no log do servidor, nunca na resposta.

| Status | Quando |
|---|---|
| 400 | Token de registro inválido, ação fora do catálogo, argumento inválido |
| 401 | Sem token, token vencido, credencial errada |
| 403 | Conta desativada, papel sem permissão |
| 409 | Limite de dispositivos, transição de estado inválida (ex.: cancelar tarefa já finalizada) |
| 500 | Bug nosso — detalhe genérico na resposta, stack no log |

**E-mail inexistente e senha errada devolvem o mesmo 401**, com a mesma mensagem.
Diferenciar permitiria enumerar contas válidas.

---

## Limite de dispositivos: dispositivo ≠ sessão

`docs/politica-login-sessao.md §2.1` diz que Admin/SuperAdmin podem ter **3
dispositivos**. A primeira implementação contava **sessões**, o que produzia isto:

```
login 1 -> 200      login 4 -> 409  ← trancado fora
login 2 -> 200         (sempre a MESMA máquina)
login 3 -> 200
```

Fechar o navegador e entrar de novo três vezes bloqueava o usuário da própria
conta sem ele nunca ter usado mais de um aparelho.

Agora o `DeviceLabel` decide: login do mesmo dispositivo **revoga a sessão
anterior e ocupa o mesmo slot**. Três dispositivos *diferentes* continuam
bloqueando o quarto. Coberto por três testes em `LoginUseCaseTests`.

---

## CORS

Em Development libera **uma** origem, `https://localhost:5021`, com
`AllowCredentials` — obrigatório para o SignalR, cujo handshake manda credencial.
Em produção o Blazor é servido pelo próprio host e CORS deixa de ser necessário.

A lista antiga tinha `5000`/`5001` (de um perfil de launch que não existe mais) e
`5173`, que é a porta do Vite e nunca foi usada por projeto nenhum daqui.

---

## Roteiro de teste manual

```bash
# 1. login
TOKEN=$(curl -s -X POST https://localhost:5021/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@etec.sp.gov.br","password":"informe123"}' \
  | python -c "import sys,json;print(json.load(sys.stdin)['accessToken'])")

# 2. máquinas
curl -s "https://localhost:5021/devices?status=Online" -H "Authorization: Bearer $TOKEN"

# 3. catálogo
curl -s https://localhost:5021/actions -H "Authorization: Bearer $TOKEN"

# 4. disparar a ação mais inofensiva do catálogo (só lê, não altera nada)
curl -s -X POST https://localhost:5021/tasks -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"action":"InformacoesDoSistema","deviceIds":["<id>"],"groupIds":null,"scheduledAt":null}'

# 5. token para registrar uma máquina
curl -s -X POST https://localhost:5021/admin/enrollment-tokens -H "Authorization: Bearer $TOKEN"
```

---

## Tempo real — conectar no DashboardHub

WebSocket não manda header `Authorization`, então o JWT vai na query string. O
servidor só lê `access_token` na rota do hub, para o token não vazar no log de
acesso das outras rotas (ver `AuthenticationSetup.cs`).

```csharp
var hub = new HubConnectionBuilder()
    .WithUrl($"https://localhost:5021/hubs/dashboard?access_token={accessToken}")
    .WithAutomaticReconnect()
    .Build();

hub.On<Guid, string, string>("EndpointStatusChanged", (deviceId, status, health) => { /* ... */ });
hub.On<TelemetryDto>("TelemetryUpdated", t => { /* ... */ });
hub.On<AlertDto>("AlertRaised", a => { /* ... */ });
hub.On<Guid, string>("TaskProgress", (taskId, status) => { /* ... */ });

// Grão de MÁQUINA — é este que faz a execução em N máquinas aparecer avançando
// em paralelo na tela. TaskProgress só dispara quando a tarefa inteira acaba.
hub.On<Guid, Guid, string, int?, string?>("ExecutionLogUpdated",
    (logId, taskId, status, durationMs, output) => { /* ... */ });

await hub.StartAsync();
```

> ⚠️ Hoje o `SignalRDashboardNotifier` publica com `Clients.All`: **todo usuário
> autenticado, inclusive Viewer, recebe telemetria e alerta do parque inteiro.**
> Filtrar por grupo exige SignalR Groups + o escopo N-N Admin↔Group, classificado
> como Fase 2 em `politica-login-sessao.md`. Precisa ser resolvido antes da tela
> do Viewer ir a produção.

---

## O que a API ainda não tem

- **Dashboard e alertas** — nenhuma rota de leitura de alerta ou métrica diária
- **Paginação** — `/devices` e `/users` devolvem tudo. Com 105 máquinas passa;
  com 1000, não
- **Reentrega de comando offline** — máquina desligada no disparo tem o log
  fechado como `Failed`; quando reconecta, não recebe o que perdeu (RF10 modela
  a fila no banco, falta o agente pedir os pendentes no `OnConnectedAsync`)
- **Interromper execução em andamento** — `POST /tasks/{id}/cancel` cancela do
  lado do Host; a máquina que já recebeu o comando continua executando. Exigiria
  um método novo em `IAgentClient`
- **Rotação de chave do agente** — `RotateKey` existe nas duas pontas e nada o
  invoca (RF13 inerte)
