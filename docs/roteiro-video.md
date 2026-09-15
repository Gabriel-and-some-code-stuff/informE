# Roteiro do vídeo — 3 minutos

> Grave em **4 clipes separados** e emende. Uma tomada única de 3 minutos você
> refaz nove vezes; quatro clipes de 45 segundos você refaz um.

## Antes de ligar a câmera

```powershell
.\informe.ps1
```

Espere o app abrir. Então confira, **nesta ordem**:

- [ ] **Equipamentos mostra 98 online / 7 offline** — se estiver tudo offline, o
      servidor não subiu com a versão nova. Rode `.\informe.ps1 -Parar` e de novo.
- [ ] **A sua máquina aparece na lista.** Se não, o agente não está rodando:
      `.\novo-agente.ps1 -Publicar` e siga o que ele imprime.
- [ ] Faça **um** disparo de teste antes de gravar, para a tela de Execuções não
      estar vazia no clipe 3.
- [ ] Feche tudo que não é o informE. Notificação do Windows aparecendo no meio
      do take custa uma regravação.

**Credenciais:**

| Quem | E-mail | Senha |
|---|---|---|
| Administrador | `admin@cps.sp.gov.br` | `informe123` |
| Professor (Lab 2) | `alexandre@cps.sp.gov.br` | `informe123` |

---

# Clipe 1 · Login e visão geral — 30s

**Faz:** abre o app, escolhe **Administrador**, digita as credenciais, entra.

**Fala:**

> "informE é uma ferramenta de gestão remota para laboratórios de escola. O
> administrador entra e já vê o parque inteiro: quantas máquinas existem,
> quantas estão no ar e quantas têm problema."

**Mostra:** os cinco números do topo — Grupos, Total, Online, Offline, Alertas.

> ⚠️ **O Dashboard usa dados de exemplo.** Se perguntarem, é massa de
> demonstração — e a próxima tela mostra dado real. Não invente que é real.

---

# Clipe 2 · Equipamentos, e a máquina de verdade — 50s

**Este é o clipe mais importante.** É o único momento em que você prova que o
sistema fala com uma máquina real.

**Faz:**

1. Menu **Equipamentos**
2. Aponta a tabela — processador, memória, disco, tempo ligado
3. **Digita o nome da sua máquina na busca**
4. Clica na linha dela → abre o painel de detalhe

**Fala:**

> "Aqui é dado real. Este notebook aqui na mesa" — *aponte para ele* — "está
> rodando o agente do informE. Processador em 42%, memória em 78%, disco em
> 96%. Isso é a leitura de agora, não uma tela de exemplo."

**Depois abra o detalhe e diga:**

> "E onde o sistema não sabe, ele diz que não sabe. O inventário de hardware não
> é coletado nesta versão, então aparece 'Não disponível' — em vez de um número
> inventado."

**Por que isso pesa:** mostra um sistema que distingue o que mediu do que não
mediu. Banca reconhece a diferença.

**Se sobrar tempo:** troque o número de linhas de 10 para 30 e mostre a
paginação. É rápido e mostra cuidado com lista grande.

---

# Clipe 3 · Executar um comando — 45s

**Faz:**

1. Menu **Execuções** → **+ Nova Execução**
2. Ação: **Informações do Sistema**
3. Máquina: **a sua**
4. **Executar**
5. Espere o resultado aparecer e **abra a saída**

**Fala:**

> "O administrador escolhe uma ação do catálogo e a máquina de destino. O
> comando vai pelo servidor até o agente, executa lá e a saída volta."

**Ao mostrar a saída:**

> "Este texto saiu da máquina agora: nome, sistema, memória livre, espaço em
> disco. Levou menos de um segundo."

**Duas coisas que valem apontar, se a fala couber:**

> "A tela não manda o script — ela manda uma escolha. O script fica no catálogo
> do servidor, então não existe como mandar comando arbitrário pela interface."

> "E dá para disparar em várias máquinas de uma vez: uma limpeza de disco em
> todo o laboratório é um clique."

> ⚠️ **Use "Informações do Sistema".** É leitura pura. Não faça limpeza de disco
> nem reinicialização na máquina que está gravando.

---

# Clipe 4 · Professor e redefinição de senha — 55s

## Parte A — o professor só vê o laboratório dele (30s)

**Faz:** sai da conta → escolhe **Viewer** → entra como
`alexandre@cps.sp.gov.br`.

**Fala:**

> "Agora como professor. O Alexandre responde pelo Laboratório 2, e é só isso
> que ele vê: as máquinas do lab dele e os alertas do lab dele. As outras nem
> aparecem."

**Mostra:** o dashboard dele com o número menor de máquinas, e depois
**Máquinas** com só as do Lab 2.

**A frase que vale dizer:**

> "E esse recorte é decidido no servidor, não na tela. Não é a interface
> escondendo — a resposta da API já vem só com o que ele pode ver."

## Parte B — redefinir a senha (25s)

**Faz:**

1. Sai → tela de login → **Esqueci a senha**
2. Digita `admin@cps.sp.gov.br` → **Enviar instruções**
3. Aparece o aviso; clique em **Redefinir senha agora**
4. Digita a senha nova duas vezes → **Redefinir senha**
5. Volta ao login e **entra com a senha nova**

**Fala:**

> "A recuperação de senha funciona ponta a ponta. Em produção o link vai para o
> e-mail institucional; nesta máquina não há servidor de e-mail, então o sistema
> mostra o link aqui mesmo — e avisa que está fazendo isso."

**Ao entrar com a senha nova:**

> "Senha trocada, e todas as sessões anteriores foram encerradas. O link vale
> uma hora e serve uma vez só."

> ⚠️ **Depois de gravar, volte a senha para `informe123`** — senão o resto do
> time e a documentação ficam desatualizados. Rode `.\informe.ps1 -Parar`, apague
> o banco e suba de novo (ver "Recomeçar do zero" no `COMO-RODAR.md`), ou refaça
> o reset escolhendo `informe123`.

---

# Fechamento — 10s

Volte para Equipamentos, com o parque na tela.

> "É isso: um servidor, um agente por máquina, e um painel que mostra o estado
> real do laboratório e executa ação remota."

---

# Contagem

| Clipe | Duração | Acumulado |
|---|---|---|
| 1 · Login e visão geral | 30s | 0:30 |
| 2 · Equipamentos e máquina real | 50s | 1:20 |
| 3 · Executar comando | 45s | 2:05 |
| 4 · Professor e senha | 55s | 3:00 |
| Fechamento | 10s | 3:10 |

**Passou 10 segundos.** Onde cortar, em ordem:

1. A paginação no clipe 2 (é enfeite)
2. As duas frases extras do clipe 3 sobre catálogo e múltiplas máquinas
3. O fechamento — a última tela já fala por si

---

# O que NÃO mostrar

Não abra o que não está pronto. Um clique em beco sem saída custa mais
credibilidade do que a funcionalidade valeria:

| Item | Estado |
|---|---|
| Grupos, Inventário, Configurações | menu sem tela |
| Agendar execução | desabilitado, com selo "em breve" |
| Abas Processos e Serviços | não existem nesta versão |

**Se alguém perguntar sobre isso:** está no plano de outubro
(`docs/plano-outubro-novembro.md`). Ter o plano escrito é resposta melhor que
improvisar.

---

# Se algo quebrar durante a gravação

**Grave também um clipe de reserva** com só o terminal do agente: o registro, a
telemetria chegando, um comando executando. Feio, mas é prova de que o sistema
funciona. Vale mais que slide.

**Se o app não abrir:** `.\informe.ps1 -Parar`, depois `.\informe.ps1`.

**Se o login recusar:** o e-mail é `@cps.sp.gov.br` (não "etec"), e verifique se
está na tela certa — Administrador para `admin`, Viewer para `alexandre`.
