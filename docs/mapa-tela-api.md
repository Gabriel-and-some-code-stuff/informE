# Mapa tela → API

> Para cada tela, o que chamar e o que já vem pronto. Atualizado em 09/09/2026.
>
> Este documento existe porque 3 dos 14 itens pedidos em
> `pendencias_backend_para_front.docx` **já estavam implementados** — o backend
> entregou e não comunicou. `docs/api-server.md` lista as rotas; aqui está o
> caminho inverso, que é o que o frontend precisa.

Base: `https://localhost:5021` · tudo autenticado com `Authorization: Bearer`
exceto `/auth/login`, `/auth/refresh`, `/auth/forgot-password`,
`/auth/reset-password` e `/agent/*`.

---

## Login

```
POST /auth/login   { email, password }
  → { accessToken, refreshToken, refreshTokenExpiresAt, userId, username, role }
```

- **O access token vale 15 minutos.** A renovação é automática no
  `InformEApiClient` (repete a chamada uma vez no 401). A tela não precisa fazer
  nada — mas **se você criar outro HttpClient, ele não terá isso.**
- 401 é o mesmo para e-mail inexistente e senha errada, de propósito
  (diferenciar permitiria enumerar contas).
- 403 = conta desativada. 409 = 4º dispositivo bloqueado (limite de 3 para
  Admin/SuperAdmin).

## Equipamentos

```
GET /devices?grupoId=&status=&busca=
  → { resumo: { total, online, offline, comProblema },
      itens: [ { id, hostname, groupName, lastIp, os,
                 status, health, role,
                 uptimeSeconds, lastSeenAt,
                 cpuPercent, ramPercent, diskPercent } ] }
```

**Mande os filtros para o servidor, não filtre no cliente.** O `resumo` é
calculado sobre os itens **filtrados** — filtrar depois desalinha os big numbers
da tabela.

| Campo | Já vem pronto |
|---|---|
| `status` | `Online` / `Offline` / `Unknown` — "o agente fala com o Host?" |
| `health` | `Saudavel` / `Aviso` / `Critico` / `Erro` — regra oficial `Device.EvaluateHealth` |
| `role` | `Aluno` / `Professor` — separa a máquina do professor |
| `cpu/ram/diskPercent` | `null` em offline → mostre `—`, **nunca `0%`** |

`status` e `health` são **independentes**: uma máquina pode ser `Online` +
`Critico`. Não derive um do outro.

```
GET /devices/{id}
  → { equipamento: { ...igual acima... },
      hardware: { cpu, gpu, ramGb, ramType, storageGb, storageType,
                  motherBoard, bios, collectedAt } | null }
```

`hardware: null` → "Não disponível". O agente **não coleta hardware**; só
máquinas do seed têm esse bloco.

## Execuções

```
GET  /actions                    → [ { kind, displayName, description } ]
GET  /tasks?limite=50            → [ ExecutionListItemDto ]  (grão = MÁQUINA)
GET  /tasks/{id}                 → { id, code, status, maquinas: [...] }
POST /tasks                      { action, deviceIds, groupIds, scheduledAt }
                                 → { taskId, code, dispatchedCount }
POST /tasks/{id}/cancel
```

- **O dropdown vem de `/actions`.** Nunca monte a lista na tela: o script é
  resolvido pelo catálogo **no servidor** a partir do `kind`. Isso é o que impede
  injeção de script arbitrário — a tela não manda script, manda uma escolha.
- `limite` conta **tarefas**, mas a resposta é por máquina: uma ação em 20
  devices vira 20 linhas.
- `criadoPor` = username de quem disparou; `null` se o usuário foi removido.
- ⚠️ **`scheduledAt` é aceito e ignorado** — a execução dispara na hora. Ver
  item 7 da auditoria.

## Alertas

```
GET /alerts?dias=7&grupoId=&categoria=&recentes=20
  → { total,
      porCategoria: { Hardware: 3, Armazenamento: 0, ... },
      historico: [ { dia, total, porCategoria } ],
      recentes: [ { id, deviceId, deviceHostname, groupName,
                    tipo, categoria, mensagem, ocorridoEm } ] }
```

Uma chamada serve o Dashboard inteiro: big number (`total`), rosca
(`porCategoria`), barras empilhadas (`historico`) e o painel de recentes.

- As **6 faixas vêm sempre**, inclusive com zero — não complete dia vazio na tela.
- `dias` é limitado a 90.
- **Não existe filtro por severidade.** O domínio não tem esse conceito.

## Grupos

```
GET  /groups   → [ { id, name, description, totalDeDispositivos } ]
POST /groups   { name, description }
```

Resumo do laboratório = `GET /devices?grupoId=X` (bloco `resumo`) +
`GET /alerts?grupoId=X` (campo `total`).

## Usuários e perfil

```
GET    /users?papel=&ativo=&busca=
POST   /users                       { username, email, password, role }
PATCH  /users/{id}                  { username?, email? }
PATCH  /users/{id}/role             { role }
PATCH  /users/{id}/active           { ativo }
GET    /me
GET    /me/sessions
DELETE /me/sessions/{sessionId}
POST   /auth/logout
```

**Matriz de permissão** (`403` fora dela):

| Quem | Pode criar |
|---|---|
| SuperAdmin | SuperAdmin, Admin, Viewer |
| Admin | **somente** Viewer |
| Viewer | ninguém |

Admin não promove ninguém ao próprio nível.

## Senha

```
POST /auth/forgot-password   { email }
POST /auth/reset-password    { token, novaSenha }
```

`/forgot-password` responde **200 mesmo para e-mail inexistente** — de propósito,
para não revelar quais contas existem. A tela `ForgotPassword.razor` hoje **não
chama nada**; é casca.

---

## O que NÃO tem endpoint

Não procure — não existe, e é decisão registrada em
`docs/pendencias-front-auditoria.md`:

| Pedido | Situação |
|---|---|
| Processos em execução | o agente não coleta |
| Serviços do Windows | o agente não coleta (caminho barato via catálogo PowerShell) |
| Histórico de métricas | repositório existe, endpoint não |
| Grupo do Viewer | use `/groups` filtrado por `OwnerId` |
| Agendamento real | `scheduledAt` é ignorado |
| Paginação | 105 máquinas não precisam |
| Severidade de alerta | não existe no domínio |

**Regra:** dado que não existe no backend não vira valor fictício na tela. Mostre
estado neutro (`—`, "Não disponível") ou esconda o campo.
