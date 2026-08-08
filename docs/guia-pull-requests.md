# Guia de Pull Requests — informE

> Documento de referencia da equipe. Ultima atualizacao: agosto/2026.

---

## Por que usar PRs em vez de commitar direto no master

Commitar direto no `master` funciona quando voce trabalha sozinho. Em equipe, o problema e que ninguem revisa o codigo antes de entrar — um bug ou uma quebra de arquitetura vai parar no master sem que ninguem perceba.

A PR cria uma **janela de revisao**: o CI roda, o code review da IA comenta, e pelo menos uma pessoa do time le o diff antes de mergear. E o que transforma um projeto de faculdade em algo que parece profissional.

---

## Fluxo completo de uma PR

### Passo 1 — Crie uma branch com nome descritivo

Antes de tocar qualquer arquivo, crie uma branch. O padrao e `tipo/o-que-voce-faz`.

```bash
# Bom
git checkout -b feat/session-revoke-method
git checkout -b fix/datetime-utcnow-entities
git checkout -b test/group-owner-id-tests

# Ruim
git checkout -b gabriel-branch
git checkout -b alteracoes
git checkout -b fix
```

Branch com nome generico e branch que ninguem sabe o que contem. Se voce nao cria uma branch, tudo que commitar vai direto ao master — o CI roda mas a IA de review nao.

---

### Passo 2 — Commits pequenos e bem descritos

Nao espere terminar tudo para commitar. Commite a cada passo logico. Use o padrao **Conventional Commits**:

```
tipo(escopo): descricao em minusculo
```

```bash
# Implementou o metodo
git add src/Host/informE.Domain/Entities/Session.cs
git commit -m "feat(Session): adicionar metodo Revoke()"

# Escreveu o teste correspondente
git add tests/informE.Domain.Tests/SessionTests.cs
git commit -m "test(SessionTests): testar que Revoke() vira IsActive para false"
```

#### Tabela de tipos

| Tipo | Quando usar | Exemplo |
|------|------------|---------|
| `feat` | Nova funcionalidade ou metodo | `feat(Session): adicionar Revoke()` |
| `fix` | Correcao de bug | `fix(User): trocar .Now por .UtcNow` |
| `test` | Adicionar ou corrigir testes | `test(SessionTests): atualizar apos mudanca de assinatura` |
| `refactor` | Reestruturar sem mudar comportamento | `refactor(Group): extrair ValidateName para metodo estatico` |
| `docs` | Documentacao | `docs: adicionar guia de testes unitarios` |
| `ci` | Pipelines e GitHub Actions | `ci: remover MAUI do workflow` |
| `chore` | Manutencao (atualizar pacotes etc.) | `chore: atualizar Microsoft.NET.Test.Sdk` |

**Regra de ouro:** a mensagem deve completar a frase _"Se aplicado, esse commit vai..."_

- `feat(Session): adicionar metodo Revoke()` → funciona
- `alteracoes gabriel` → nao funciona

---

### Passo 3 — Suba a branch para o GitHub

```bash
git push origin feat/session-revoke-method
```

Na primeira vez que voce faz isso com uma branch nova, o GitHub mostra um aviso amarelo na pagina do repositorio com o botao **"Compare & pull request"**. Clique nele.

---

### Passo 4 — Preencha titulo e descricao

**Titulo:** segue Conventional Commits — `feat(Session): adicionar Revoke() e IsExpired()`

**Descricao:** explica o *por que*, nao o *o que* (o *o que* esta no diff). Template recomendado:

```
## O que muda
Breve paragrafo explicando o que foi feito.

## Por que
O que motivou essa mudanca (bug, requisito funcional, decisao de arquitetura).

## O que ficou de fora
Algo que poderia ter entrado mas foi deixado para depois — e por que.

## Como testar
Passos para o revisor verificar se a mudanca funciona.
```

#### Exemplo de PR bem descrita

```
feat(Session): adicionar Revoke() e IsExpired()

## O que muda
Adiciona dois metodos ao dominio de Session:
- Revoke(): marca IsActive = false
- IsExpired(): retorna true se ExpiresAt < DateTimeOffset.UtcNow

## Por que
RF07 exige que o servidor invalide sessoes ativas quando o usuario
fizer logout. Sem Revoke(), nao havia como marcar a sessao como
inativa no Domain.

## O que ficou de fora
Session.Touch() (atualizar LastSeenAt) fica para quando implementar
o AgentHub — nao faz sentido sem o hub pronto.

## Como testar
dotnet test tests/informE.Domain.Tests --filter SessionTests
```

#### Exemplo de PR que nao ajuda ninguem

```
alteracoes

alteracoes no projeto
```

Quem vai revisar essa PR nao sabe o que mudou, nao sabe se quebra algo, nao sabe como testar. E garantia de merge sem revisao real.

---

### Passo 5 — CI e AI review rodam automaticamente

Apos abrir a PR, dois checks aparecem automaticamente:

1. **CI (GitHub Actions)** — faz build do projeto e roda todos os testes. Se falhar, clique em "Details", leia o erro, corrija na mesma branch e faca push. A PR atualiza sozinha, o CI roda de novo.

2. **AI Review** — o agente le o diff dos arquivos `.cs` e posta um comentario com observacoes sobre arquitetura, seguranca e boas praticas. Leia antes de pedir review humano.

**Nao peca review humano enquanto o CI estiver vermelho.** Corrija primeiro.

---

### Passo 6 — Pelo menos uma pessoa do time revisa

Na barra lateral direita da PR, em **Reviewers**, adicione pelo menos um integrante. A revisao nao precisa ser longa.

**O que verificar ao revisar:**

| Verificar | Exemplo pratico |
|-----------|----------------|
| O codigo faz o que o titulo da PR diz? | Se o titulo e `feat(Session): Revoke()`, o metodo `Revoke()` existe? |
| Tem algum bug obvio? | Null nao tratado, caso de borda ignorado |
| O metodo novo tem pelo menos um teste? | Se sim, o teste esta na mesma PR? |
| Violou a Onion? | Domain referenciando Infrastructure ou Application? |
| Alguma data usando `.Now` em vez de `.UtcNow`? | Risco de bug de fuso horario |
| O CI passou? | Se nao, por que? |

**O que nao fazer ao revisar:**

- Nao corrija estilo subjetivo ("prefiro aspas simples") — isso e bikeshedding
- Nao peca para refatorar o que nao esta no escopo da PR
- Nao aprove sem ler — "LGTM" sem leitura e pior do que nao revisar

**Como responder aos comentarios:**

- **Concordou:** corrija o codigo, faca um novo commit na mesma branch, responda "feito" no comentario
- **Discordou:** argumente no comentario — a discussao fica registrada e vira historico da decisao da equipe
- **Nao e prioridade agora:** responda que vai para uma issue futura; nao deixe comentario sem resposta

---

### Passo 7 — Merge com Squash

Quando o CI passou e pelo menos uma pessoa aprovou, use o botao **"Squash and merge"** — nao "Merge commit" nem "Rebase and merge".

Squash merge junta todos os commits da branch em um unico commit no master. O historico fica limpo e cada linha do `git log` corresponde a uma feature ou fix completo.

Apos o merge, delete a branch. O GitHub mostra um botao **"Delete branch"** automaticamente logo apos o merge. Manter branches mortas polui o repositorio.

---

### Passo 8 — Sincronize o master local

```bash
git checkout master
git pull
```

Qualquer nova feature comeca a partir daqui. Nunca reaproveite uma branch velha.

---

## Resumo do ciclo

```
git checkout -b tipo/descricao
    ↓
commits pequenos (feat, fix, test...)
    ↓
git push origin tipo/descricao
    ↓
abrir PR com titulo e descricao
    ↓
CI passa + AI review lido
    ↓
pelo menos 1 pessoa aprova
    ↓
Squash and merge
    ↓
delete branch
    ↓
git checkout master && git pull
```

---

## Como revisar uma PR na pratica (passo a passo no GitHub)

1. Acesse a aba **Pull requests** no repositorio
2. Clique na PR que quer revisar
3. Va na aba **Files changed** — voce ve o diff completo
4. Para comentar em uma linha especifica: passe o mouse sobre o numero da linha e clique no `+` que aparece
5. Escreva o comentario e clique em **Start a review** (nao "Add single comment" — isso posta imediatamente, sem dar chance de revisar tudo primeiro)
6. Quando terminar de ler todo o diff, clique em **Review changes** (canto superior direito)
7. Escolha uma das tres opcoes:
   - **Comment** — apenas comenta, nao aprova nem rejeita
   - **Approve** — aprova, PR pode ser mergeada
   - **Request changes** — rejeita ate as correcoes serem feitas
8. Clique em **Submit review**

---

## Checklist antes de abrir uma PR

- [ ] Estou em uma branch — nao no master
- [ ] O CI passou localmente (`dotnet test tests/...`)
- [ ] O titulo segue Conventional Commits
- [ ] A descricao explica o *por que* da mudanca
- [ ] Cada novo metodo tem pelo menos um teste na mesma PR
- [ ] Nao tem `.Now` onde deveria ser `.UtcNow`
- [ ] Nao tem dependencia do Domain para Infrastructure ou Application
