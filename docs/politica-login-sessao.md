# Política de Login e Sessão — informE

## Contexto

**informE é on-premise: uma instância por cliente.** Cada instalação serve OU uma
escola OU uma empresa, nunca ambas, nunca interligadas — o servidor roda dentro da
rede do cliente, um banco de dados por vez. Não existe conceito de múltiplas
organizações compartilhando a mesma instância (nenhum `Tenant`/`Organization` existe
no Domain, e não deveria existir — não é esse o produto).

Este documento revisa e **substitui** a regra de sessão descrita em
`ARCHITECTURE.md §4` ("máx 3 refresh tokens ativos, 4º login revoga o mais antigo,
expira em 7 dias de inatividade"), que hoje é única para todos os usuários. A
política abaixo diferencia por papel — ver Seção 4 para o detalhamento da mudança.

## 1. Perfis e escopo nesta instância

- **Super Admin**: acesso irrestrito a tudo dentro desta instância.
- **Administrador**: nesta versão, enxerga todos os `Group`s desta instância. A
  diferença para Super Admin é não poder gerenciar outros Admins/Super Admins (ver
  matriz abaixo) — não uma diferença de escopo de dados. Escopo granular por
  `Group` (um Admin ver só os Groups sob sua responsabilidade) é **Fase 2**: hoje
  `Group.OwnerId` é 1-para-1 com um único `User`, então "Admin escopado a alguns
  Groups" exigiria uma associação N-N nova (`GroupAdmin` ou similar), que não existe
  e não está no plano de 4 semanas atual.
- **Usuário Comum**: acesso apenas aos próprios dados/máquinas. O papel semântico
  varia pelo contexto da instância — funcionário (empresa) ou professor (escola) —
  mas o dado técnico (`UserRole.Viewer`) é o mesmo enum em ambos os casos.

**Matriz de CRUD e senha:**

| Quem | Pode gerenciar (CRUD) | Pode ver senha? |
|---|---|---|
| Super Admin | Qualquer perfil, inclusive Administradores | Nunca |
| Administrador | Usuários Comuns desta instância | Nunca |

Todo reset de senha feito por terceiros (Super Admin sobre Admin, ou Admin sobre
Usuário Comum) gera uma **senha temporária ou link de definição** — a senha atual do
usuário nunca é exibida a ninguém além dele mesmo.

## 2. Sessão por perfil

### 2.1 Super Admin e Administrador — até 3 dispositivos, bloqueio do 4º

- Até **3 dispositivos** registrados simultaneamente por usuário.
- Tentativa de registrar um **4º dispositivo** é bloqueada até que um dos três
  existentes seja revogado — diferente da regra genérica hoje em
  `ARCHITECTURE.md §4` ("revoga o mais antigo automaticamente"), que deixa de se
  aplicar a este papel. Ver Seção 4.
- Ao logar em um dispositivo novo, um dropdown pergunta: *"Este é seu dispositivo
  principal?"*
  - Marcar como principal dispara uma notificação por e-mail, com opção de revogar
    caso não tenha sido o próprio usuário.
  - **Dispositivo principal**: sessão persiste até o próximo reset de senha (ver
    Seção 3 — não há mais reset mensal forçado), até 15 dias de inatividade, ou
    até revogação manual — o que ocorrer primeiro.
  - **Dispositivos secundários**: login obrigatório a cada abertura do software;
    sessão encerra ao fechar o app.
- **MFA obrigatório** para login em qualquer dispositivo — **Fase 2** (ver
  Seção 5). Biblioteca recomendada quando for implementado: `OtpNet` (TOTP,
  RFC 6238, sem dependências externas).

**Modelagem necessária** (nenhuma implementada ainda): estender `Session`
(`src/Host/informE.Domain/Entities/Session.cs`) com dois campos —

```csharp
public string? DeviceLabel { get; set; } // ex.: "Chrome — Windows 11"
public bool IsPrimary { get; set; }      // resposta ao "dispositivo principal?"
```

`ponytail:` `Session` já é, na prática, um "slot de dispositivo logado" — nasce no
login, morre no logout/expiração, e a regra de "3 ativos" já existe em
`ARCHITECTURE.md §4`. Não há necessidade de uma entidade `TrustedDevice` separada;
isso duplicaria o que `Session` já faz.

Painel de dispositivos ativos com revogação remota: **Fase 2** — precisa de UI nova
+ endpoints novos, nenhum dos dois existe hoje.

### 2.2 Usuário Comum — empresa (funcionário) — 1 sessão, kick automático

- Apenas **1 sessão ativa por vez**. Login em uma nova máquina invalida
  automaticamente a sessão anterior ("kick").

**Mecanismo do kick (Fase 1 — sem WebSocket):**

1. O JWT ganha um claim `sid` com o `Session.Id` (hoje o token só carrega `sub`,
   `email`, `role`, `jti` — ver `JwtTokenService.cs`).
2. Todo request autenticado verifica `Session.IsActive` no banco antes de
   processar. Um novo login marca a sessão anterior como `IsActive = false` (já
   existe: `Session.Revoke()`).
3. Se a checagem falhar, o servidor responde 401 e o cliente força novo login,
   exibindo *"Sua conta foi acessada em outro dispositivo"*.
4. Isso cabe dentro de `feat/server-auth-endpoints` (Semana 3 do plano de 4
   semanas) — **não depende de `DashboardHub`/SignalR**, que ainda não existe.

**O que fica para Fase 2**: o *toast* de "sessão encerrada" aparecendo **sem** o
usuário precisar fazer alguma ação — isso é o que o WebSocket dá, e depende de
`DashboardHub` (Semana 3, ainda não iniciado). Quando o hub existir, adicionar um
método `SessionRevoked()` a `IDashboardClient` é um incremento pequeno.

**Cortado do escopo**: o heartbeat/polling de 30-60s do rascunho original foi
removido — é complexidade redundante. A checagem por request já detecta o kick na
próxima ação do usuário, que normalmente ocorre bem antes de 30-60s.

- Todo evento de kick vira `AuditLog` (`Action = "session_kicked"`, 14 caracteres —
  cabe no limite hoje existente de 30 caracteres em `AuditLog.Action`, ver Seção 4).
- Ao ser kickado, o app local limpa o token, mas mantém o nome de usuário salvo
  para autocomplete — nunca a senha.

### 2.3 Usuário Comum — escola (professor) — múltiplas sessões, timeout

- **Múltiplas sessões simultâneas** permitidas, dada a alta rotatividade entre
  salas.
- **Timeout de inatividade: 30 minutos.** A sessão trava e exige senha para
  retomar. O valor é deliberadamente mais restritivo (não 60min) por ser um
  laboratório compartilhado com alunos, potencialmente menores — LGPD/ECA pesam
  contra deixar o painel aberto por mais tempo sem vigilância.
- **Alerta de contagem de sessões simultâneas: limite 10.** Acima disso, gera um
  alerta para o Admin da escola (não bloqueia) — pode indicar compartilhamento de
  credencial. O valor de 10 corresponde ao tamanho típico de uma turma/lab de Etec.

Ambos os itens são **Fase 1**: o timeout é client-side (temporizador local, sem
invalidar o JWT), e o alerta reaproveita o padrão já decidido para a entidade
`Alert` em `ARCHITECTURE.md §3.7` — basta um `AlertType` novo (`HighSessionCount`).
Nenhum dos dois depende de `AgentHub`/`DashboardHub`.

## 3. Regras gerais

### 3.1 Senha

- **Sem reset mensal forçado.** Justificativa:
  1. Nenhuma exigência de compliance foi encontrada em qualquer documento do
     projeto — é prática do rascunho original, não requisito de cliente.
  2. **NIST SP 800-63B** recomenda explicitamente contra rotação periódica forçada,
     exceto evidência concreta de comprometimento — é a referência técnica atual.
  3. Com zero horas livres no cronograma, investir em **MFA** (Fase 2) tem retorno
     de segurança muito maior por hora de trabalho do que rotação mensal.
- **Sem verificação Have I Been Pwned na Fase 1.** informE é "100% on-premise, sem
  nuvem" — chamar uma API externa significa o servidor do cliente fazer uma
  requisição de saída à internet, o que tensiona com esse posicionamento e pode
  simplesmente ser bloqueado pelo firewall/proxy de uma Etec. Fica como opcional de
  Fase 2, condicionado à rede do cliente permitir egress HTTPS.
- **Histórico de reuso de senha (últimas N): cortado.** Só faz sentido existir se
  houver rotação forçada disparando trocas frequentes; sem rotação, seria código
  sem uso real.
- Armazenamento apenas como hash — já implementado: **Argon2id**
  (`PasswordHasher.cs`, t=4, m=64MB, p=2), nunca reversível, nunca em log.

### 3.2 Login obrigatório a cada abertura

Regra padrão para todos os perfis, exceto a exceção explícita e temporária do
"dispositivo principal" de Super Admin/Administrador (Seção 2.1).

### 3.3 Autocomplete

- Nome de usuário pode ser salvo/autocompletado. Senha nunca é salva ou
  autocompletada.
- Em dispositivos compartilhados (laboratórios de escola), desabilitar também o
  autocomplete de usuário, para não expor nomes de login válidos a qualquer pessoa
  que sente na máquina.

### 3.4 Proteção contra força bruta

- Bloqueio temporário após 5 tentativas de login incorretas, com 15 minutos de
  bloqueio.
- Alerta automático para o Admin/Super Admin responsável em caso de bloqueio.

**Modelagem necessária**: dois campos novos em `User`
(`src/Host/informE.Domain/Entities/User.cs`) —

```csharp
public int FailedLoginAttempts { get; set; }
public DateTimeOffset? LockedUntil { get; set; }
```

Uma tabela separada de tentativas seria mais auditável, mas custa mais (entidade +
repositório + escrita a cada tentativa) sem necessidade real na escala de uma
escola/PME — os dois campos cabem na mesma migration que os campos de sessão da
Seção 2.1.

**Nota de escopo**: a lógica (incrementar contador, checar `LockedUntil` antes de
autenticar) é uma extensão de `LoginUseCase`, tarefa já alocada à Faggian na Semana
1 do plano de 4 semanas — cabe, mas é ~1h a mais de trabalho real, não "de graça".
Vale nomear essa extensão explicitamente no planejamento da semana, em vez de
assumir que vai acontecer sozinha.

Tentativa falha vira `AuditLog` (`Action = "login_failed"`, 12 caracteres).

### 3.5 Revogação por desligamento

Ao desativar uma conta (demissão, fim de contrato/matrícula), todas as sessões
ativas daquele usuário devem ser encerradas **imediatamente** — não esperar o
próximo login ou heartbeat. Mecanismo: marcar todas as `Session.IsActive = false`
daquele `UserId` no mesmo request de desativação.

### 3.6 Auditoria e conformidade

- Log completo de: login, logout forçado, registro/revogação de dispositivo, reset
  de senha, tentativas de força bruta, kick de sessão.
- Atenção à LGPD (dados pessoais de funcionários e professores) e ao ECA quando o
  sistema tocar, ainda que indiretamente, dados de menores em ambiente escolar.

## 4. Divergências com ARCHITECTURE.md (o que este documento substitui)

- **`Session.ExpiresAt` hardcoded em 6 horas** (`Session.cs:31`) — contradiz os "7
  dias de inatividade" já documentados em `ARCHITECTURE.md §4`. Isso é um **bug a
  corrigir**, não um comportamento a preservar: o valor precisa refletir a política
  real por perfil desta Seção 2, não um número fixo esquecido no construtor.
- **"Revoga o mais antigo automaticamente" deixa de ser uma regra única.** Passa a
  variar por `UserRole`: Super Admin/Admin bloqueiam o 4º dispositivo (Seção 2.1);
  Usuário Comum-empresa faz kick automático do único ativo (Seção 2.2); Usuário
  Comum-escola não tem limite (Seção 2.3). `ARCHITECTURE.md §4` deve ganhar uma nota
  apontando para este documento como a fonte de verdade sobre sessão.
- **`AuditLog.Action` descarta silenciosamente strings de 30+ caracteres**
  (`AuditLog.cs:26` — `if (action.Length < 30) Action = action;`). Toda ação nova
  proposta neste documento (`session_kicked`, `login_failed`, etc.) foi verificada
  para caber nesse limite, mas o limite em si é frágil e vale corrigir (lançar
  exceção em vez de descartar) na próxima vez que `AuditLog` for tocado.

## 5. Fase 1 (cabe no plano de 4 semanas) vs Fase 2 (backlog pós-TCC)

| Item | Fase | Justificativa |
|---|---|---|
| Sessão diferenciada por perfil (3 bloqueia / 1 kick / N ilimitado) | 1 | Extensão de `LoginUseCase` (Semana 1) + 2 campos novos em `Session` |
| Kick via checagem HTTP por request | 1 | Não depende de hub; cabe em `feat/server-auth-endpoints` (Semana 3) |
| Timeout de inatividade (professor, 30min) | 1 | Client-side, sem dependência de backend novo |
| Alerta de contagem de sessões (≥10) | 1 | Reaproveita padrão `Alert` já decidido |
| Lockout por força bruta | 1 (com ressalva de escopo) | Extensão pontual de tarefa já alocada — nomear a troca |
| Correção do bug `Session.ExpiresAt` (6h→política real) | 1 (bugfix) | Dívida técnica já documentada no próprio código |
| Auditoria login/logout/kick/lockout | 1 | `AuditLog` já existe — atenção ao teto de 30 caracteres |
| MFA/TOTP (`OtpNet`) | 2 | Zero slot livre no cronograma; qualquer corte mínimo desloca outra tarefa |
| Push em tempo real do kick (`DashboardHub`) | 2 | Depende de hub ainda não implementado (Semana 3) |
| Painel de dispositivos ativos + revogação manual (UI) | 2 | Precisa de UI + endpoints novos |
| Verificação Have I Been Pwned | 2 | Tensiona com "on-premise sem nuvem"; egress pode ser bloqueado pelo cliente |
| Escopo de Admin por Group (N-N) | 2 | `Group.OwnerId` é 1-para-1 hoje; exige entidade nova |
| Reset mensal forçado + histórico de reuso | **cortado** (nem 1 nem 2) | Recomendação técnica é não implementar — ver Seção 3.1 |

## 6. Pontos em aberto

- Ferramenta de MFA definitiva — `OtpNet` é a recomendação para quando a Fase 2
  começar, mas não foi validada em código ainda.
- Se e quando `DashboardHub` existir (Semana 3), confirmar o formato exato do
  evento `SessionRevoked()` em `IDashboardClient` junto de quem implementar o hub.
