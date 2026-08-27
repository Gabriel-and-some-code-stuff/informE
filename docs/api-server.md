# API do Server

> Endpoints REST + hubs SignalR. Atualizado em 27/08/2026.
> Base de desenvolvimento: `http://localhost:5000`

Para explorar interativamente, suba o Server em Development e abra
`/openapi/v1.json` — a especificação sai gerada dos próprios endpoints.

---

## Autenticação

Tudo exige `Authorization: Bearer <access_token>`, exceto `/auth/login`,
`/agent/enroll` e `/`.

```bash
curl -X POST http://localhost:5000/auth/login \
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

Em Development libera `localhost:5000`, `localhost:5001` e `localhost:5173`, com
`AllowCredentials` — obrigatório para o SignalR, cujo handshake manda credencial.
Em produção o Blazor é servido pelo próprio host e CORS deixa de ser necessário.

---

## Roteiro de teste manual

```bash
# 1. login
TOKEN=$(curl -s -X POST http://localhost:5000/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@etec.sp.gov.br","password":"informe123"}' \
  | python -c "import sys,json;print(json.load(sys.stdin)['accessToken'])")

# 2. máquinas
curl -s "http://localhost:5000/devices?status=Online" -H "Authorization: Bearer $TOKEN"

# 3. catálogo
curl -s http://localhost:5000/actions -H "Authorization: Bearer $TOKEN"

# 4. disparar a ação mais inofensiva do catálogo (só lê, não altera nada)
curl -s -X POST http://localhost:5000/tasks -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"action":"InformacoesDoSistema","deviceIds":["<id>"],"groupIds":null,"scheduledAt":null}'

# 5. token para registrar uma máquina
curl -s -X POST http://localhost:5000/admin/enrollment-tokens -H "Authorization: Bearer $TOKEN"
```

---

## O que a API ainda não tem

- **`POST /auth/refresh`** — sem ele, sessão de 15 min na prática
- **Logout** — `RevokeSessionUseCase` existe, endpoint não
- **CRUD de usuário** — os use cases existem (`CreateUser`, `SetUserActive`,
  `ChangeUserRole`), faltam as rotas
- **Redefinição de senha** — idem (`RequestPasswordReset`, `ResetPassword`)
- **Dashboard e alertas** — nenhuma rota de leitura de alerta ou métrica diária
- **Paginação** — `/devices` devolve tudo. Com 105 máquinas passa; com 1000, não
