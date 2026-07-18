# Análise do Banco de Dados + Docker Compose

Análise do schema real (`docs/db/estrutura_informe_db.sql`, dump PostgreSQL 18.3),
do relatório de validação (`Validação BD2.docx`, 16 evidências, 15–16/07/2026) e do
`docker-compose.yml` que o time usava. Fecha com **ponderações** e **perguntas** que
precisam de decisão antes de avançar.

---

## 1. O relatório de validação (docx)

O documento é a entrega de **BD2**: 16 prints provando que as restrições do banco
funcionam. Resumo do que foi validado com sucesso:

| # | Teste | Resultado |
|---|---|---|
| 1 | ENUM em `tasks` rejeita valor fora da lista | ✅ |
| 2 | Agendamento vs "agora" (default) numa task em execução | ✅ |
| 3 | FK inexistente (usuário com cargo que não existe) é barrada | ✅ |
| 4 | Inserção de usuário + cargo + sessão | ✅ |
| 5 | Inserção de hardware → device → grupo (cadeia de FKs) | ✅ |
| 6 | UPDATE com ENUM inválido é barrado | ✅ |
| 7 | `NOT NULL` respeitado | ✅ |
| 8 | UNIQUE (email duplicado) barrado | ✅ |
| 9 | Sessão para usuário inexistente barrada (FK) | ✅ |
| 10 | Não deleta "pai" com "filhos" (proteção de órfãos) | ✅ |
| 11 | `varchar(100)` estoura e barra o registro | ✅ |
| 12 | Defaults automáticos (data/hora na inserção) | ✅ |
| 13–16 | 4 consultas SELECT (PC→grupo, hardware por PC, sessões por usuário, responsável) | ✅ |

**Ponderação:** o design está **sólido e testado**. Integridade referencial, ENUMs,
NOT NULL, UNIQUE, defaults e proteção de órfãos foram exercitados de verdade. Isso não
é um rascunho — é um schema validado. O trabalho agora é **portá-lo pra dentro da
arquitetura** (EF Core Code-First + as decisões que travamos), não recomeçar.

---

## 2. O schema real vs. o que a print sugeria

O SQL entregue é **mais completo** do que a print inicial fazia parecer. Vários "furos"
que eu havia apontado **já estavam resolvidos**:

| Item que eu marquei como furo | Situação real no SQL |
|---|---|
| `id_device` em `task_execution_logs` | ✅ **já existe** (com FK) |
| Campos de sessão (token_hash, expires_at, last_seen) | ✅ **já existem** |
| Status/last_seen do device | ✅ `is_online` + `last_seen` existem |
| UNIQUE em hostname/mac/email/username | ✅ **todos presentes** |
| CASCADE nas tabelas de junção | ✅ presente |

Crédito ao time: o banco cobriu quase tudo. O que **realmente falta** é menor e listado
abaixo.

---

## 3. Divergências entre o schema e a arquitetura travada

Aqui está a tensão real. O schema foi desenhado para a matéria de BD; a arquitetura
tomou decisões que **mudam alguns pontos**. Cada linha é um ponto de decisão.

### 3.1 Chaves primárias — INT+uuid vs uuid-only

- **Schema:** cada tabela tem `id_* INT GENERATED ALWAYS AS IDENTITY` (PK) **e** um
  `uuid_* uuid DEFAULT gen_random_uuid()` como coluna secundária.
- **Decisão travada:** **só uuid como PK** (INT dropado).
- **O que fizemos:** as entidades EF usam `Guid Id` com `gen_random_uuid()` como PK
  única. A migration gerada **não tem colunas INT**.
- ⚠️ **Isto muda o schema entregue na BD.** Ver Pergunta 1.

### 3.2 ENUMs de 1 caractere → texto legível

- **Schema:** `patent_name('V','A','SA')`, `status_log('S','E','T')`,
  `status_task('P','IE')`, `group_state('Y','N')`, etc.
- **O que fizemos:** enums C# legíveis (`UserRole.Admin`, `TaskStatus.Failed`)
  gravados como **texto** via `HasConversion<string>()`. Some o "decifrar 'IE' =
  in execution"; o banco fica auto-explicativo.
- ⚠️ Muda os tipos ENUM do Postgres. Ver Pergunta 2.

### 3.3 `roles` como tabela vs enum

- **Schema:** tabela `roles` separada, referenciada por `users.id_role`.
- **O que fizemos:** `UserRole` é **enum em C#** — sem tabela `roles`. Três cargos
  fixos (Viewer/Admin/SuperAdmin) não justificam uma tabela + JOIN.
- ⚠️ Se o produto precisar de **cargos customizados criados pelo admin**, aí a tabela
  volta. Ver Pergunta 3.

### 3.4 Auth do agente — **o furo crítico**

- **Schema:** **não existe** nenhuma coluna/tabela pra autenticar o agente.
- **Produto exige:** `devices.agent_key_hash` + `key_rotated_at` e uma tabela
  `enrollment_tokens`. Sem isso os RF05–RF08 (enrollment → chave rotativa → hub) não
  fecham, e o pilar de Segurança cai.
- **O que fizemos:** adicionado nas entidades + migration (`enrollment_tokens` criada,
  `agent_key_hash`/`key_rotated_at` em `devices`).

### 3.5 `is_online` persistido vs status derivado

- **Schema:** `is_online ENUM('Y','N')` gravado no banco.
- **Decisão travada:** status **derivado ao vivo** do connection registry
  (telemetria não persiste). Mantemos só `last_seen` + um `Status` (enum) para o
  último estado conhecido, mas a verdade em tempo real vem do hub.
- Ver Pergunta 4.

---

## 4. Bugs e detalhes do schema

Coisas pequenas, mas que valem correção no port:

1. **`users_id_role_fkey ... ON DELETE SET NULL`, mas `id_role` é `NOT NULL`.**
   Contradição: se algum dia deletarem um role, o `SET NULL` viola o `NOT NULL` e o
   banco dá erro. No nosso modelo isso some (role vira enum).
2. **Typo no nome da constraint:** `task_exexution_logs_*` ("exexution"). Cosmético,
   mas nasce errado. Na migration EF já sai como `task_execution_logs`.
3. **`task_execution_logs.id_user`** — existe no schema, mas nosso `MachineTask` já
   tem `CreatedByUserId`. Provável duplicação. Ver Pergunta 5.
4. **`devices.id_user`** — FK ambígua. Dono da máquina? Quem fez enroll? Último
   logado? Nosso modelo liga device→grupo, não device→user. Ver Pergunta 6.
5. **`info_devices`** não tem `id_user` (a print sugeria que tinha). Consistente com
   nosso `DeviceInfo` (1-1 com Device, sem dono próprio). ✅

---

## 5. Docker Compose — o que precisa mudar

O `docker-compose.yml` que o time usava tem heranças de template de aula:

```yaml
# O que veio:
version: '3.7'                       # chave 'version' obsoleta no Compose v2
image: bitnami/postgresql:latest     # imagem bitnami (mais pesada, opinativa)
POSTGRESQL_DATABASE=polls            # 'polls' = sobra do tutorial Django!
ALLOW_EMPTY_PASSWORD=yes             # contradiz o password logo acima
ports: '5433:5432'                   # porta fora do padrão
volumes: ./dados_bd:/bitnami/...     # bind-mount → dados crus versionados no disco
```

Problemas: `database=polls` (nome errado, sobra de template), `ALLOW_EMPTY_PASSWORD`
junto de uma senha (contraditório), bind-mount que faz o Postgres cuspir arquivos
binários na pasta do projeto (é a origem da pasta `DB1607_OK/dados_bd/` — **não deve
ir pro git**).

**O que está no repo agora** (`docker-compose.yml`):

```yaml
image: postgres:16                   # imagem oficial, enxuta
POSTGRES_DB: informe                 # nome certo
POSTGRES_USER/PASSWORD: informe/...  # coerente com a connection string
ports: '5432:5432'                   # padrão
volumes: informe_pgdata (named)      # volume nomeado, não polui o repo
healthcheck: pg_isready              # setup-dev.ps1 espera ficar 'healthy'
```

**Por que não usar o dump como fonte da verdade?** Porque adotamos **EF Code-First**:
o banco nasce das entidades C# via migrations, não de um `.sql` rodado à mão. Isso dá
versionamento (cada mudança de modelo vira uma migration), previne drift e é a decisão
travada. O `.sql` fica em `docs/db/` como **referência histórica** do design da BD.

---

## 6. Perguntas para o time (precisam de decisão)

> Estas não são detalhes — mudam o que vai pro banco. Respondam antes do Guilherme
> fechar as migrations.

1. **PK uuid-only vs INT+uuid.** A nota de BD2 depende do schema **exato** entregue
   (com PK INT)? Se sim, há tensão: o EF Code-First gera uuid-only. Opções: (a)
   migrar pra uuid-only e a BD entregue vira "versão 1" documentada; (b) manter
   INT+uuid e eu ajusto as entidades. **Recomendo (a)** — foi o que travamos e é mais
   limpo, mas a decisão é de vocês porque envolve nota.
2. **ENUMs legíveis.** Os códigos de 1 letra ('IE', 'S/E/T') foram exigência da
   matéria ou preferência? Posso gravar texto legível ('Running', 'Succeeded')?
3. **`roles`: tabela ou enum?** Cargos são fixos (Viewer/Admin/SuperAdmin) ou o admin
   vai criar cargos novos? Fixos → enum (já feito). Customizáveis → volto a tabela.
4. **`is_online`: persistir ou derivar?** Confirmam status derivado ao vivo do
   registry (mantendo só `last_seen`)? Ou querem o Y/N gravado?
5. **`task_execution_logs.id_user`** — quem é? Já temos `MachineTask.CreatedByUserId`.
   É a mesma pessoa (duplicado) ou um "executou como" diferente?
6. **`devices.id_user`** — o que significa? Precisamos dessa FK ou o vínculo
   device→grupo basta?

---

## 7. O que já foi feito neste port

- ✅ 10 entidades + 6 enums no `Domain` (uuid PK, UTC).
- ✅ `AppDbContext` + 10 configurações Fluent API (nomes de tabela/coluna casando com
  o schema validado: `users`, `devices`, `info_devices`, `task_execution_logs`,
  `devices_softwares`, `devices_tasks`, `groups`, `softwares`, `audit_logs`).
- ✅ Migration `InitialCreate` gerada e verificada (10 tabelas + joins + FKs + uniques
  + `enrollment_tokens` novo).
- ✅ Auth do agente adicionada (`agent_key_hash`, `key_rotated_at`, `enrollment_tokens`).
- ✅ `docker-compose.yml` corrigido + healthcheck.
- ✅ `setup-dev.ps1` agora sobe o Postgres e aplica as migrations sozinho.
- ⏳ **Pendente das respostas acima:** ajustar enums/roles/is_online conforme decisão.
- ⏳ **Não rodado aqui:** `dotnet ef database update` (o Docker/WSL da máquina de dev
  estava quebrado no momento). A migration está pronta; roda no primeiro ambiente com
  Docker de pé.
