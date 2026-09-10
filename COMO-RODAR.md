# Como rodar o informE

Guia para **qualquer pessoa**, mesmo sem experiência com programação.
Você vai digitar dois comandos. Nada mais.

**Precisa de:** um computador com Windows 10 ou 11.

---

# Parte 1 — Preparar (só na primeira vez, ~5 min)

## 1.1 Baixar o informE

Se você recebeu uma **pasta** com o projeto, pule para o passo 1.2.

Se não, [baixe o ZIP aqui](https://github.com/Gabriel-and-some-code-stuff/informE/archive/refs/heads/master.zip),
clique com o botão direito no arquivo baixado → **Extrair tudo** → escolha a
Área de Trabalho.

Você vai ficar com uma pasta chamada `informE`.

## 1.2 Instalar o .NET

O informE foi feito com uma ferramenta da Microsoft chamada .NET. Para instalar:

1. Aperte a tecla **Windows**, digite `powershell` e aperte **Enter**
2. Cole o comando abaixo (Ctrl+V) e aperte **Enter**:

```
winget install Microsoft.DotNet.SDK.10
```

3. Espere terminar (alguns minutos)
4. **Feche essa janela** — importante, senão o próximo passo não funciona

> **Não sabe se já tem?** Não faz mal instalar de novo: ele avisa que já existe
> e não faz nada.

---

# Parte 2 — Ligar o informE

## 2.1 Abrir o terminal na pasta do informE

1. Abra a pasta `informE` no Explorador de Arquivos
2. Clique na **barra de endereço** (onde aparece o caminho da pasta)
3. Apague o que está escrito, digite `powershell` e aperte **Enter**

Vai abrir uma janela preta ou azul, já apontando para a pasta certa.

## 2.2 Ligar

Cole este comando e aperte **Enter**:

```
powershell -ExecutionPolicy Bypass -File informe.ps1
```

Na primeira vez leva de **3 a 5 minutos** — ele instala o banco de dados,
prepara tudo e abre o programa. Nas vezes seguintes, menos de um minuto.

**Vai aparecendo o que ele está fazendo.** Pontinhos na tela significam
"estou trabalhando, espere". É normal.

## 2.3 Duas perguntas que o Windows pode fazer

**"Deseja permitir que este aplicativo faça alterações?"**
→ Clique em **Sim**.

**"Deseja permitir comunicação nas redes?"**
→ Marque **Redes privadas** e clique em **Permitir acesso**.

> Essa segunda é importante: sem ela, os outros computadores não conseguem
> conversar com o informE.

## 2.4 Entrar

O programa abre sozinho. Na tela de login:

| Campo | O que digitar |
|---|---|
| E-mail | `admin@cps.sp.gov.br` |
| Senha | `informe123` |

Pronto. Você está dentro.

---

# Parte 3 — Monitorar outros computadores

Esta parte é opcional. Faça só se quiser ver **mais de um** computador na tela.

Chamamos de **agente** o programinha que fica no computador monitorado. Ele não
tem janela, não incomoda ninguém — só informa como a máquina está.

## 3.1 No computador principal: gerar o convite

Na mesma janela preta, cole:

```
powershell -ExecutionPolicy Bypass -File novo-agente.ps1 -Publicar
```

Ele vai mostrar, na tela, três coisas:

1. Uma pasta criada: `dist\agente`
2. Um **token** — é uma senha de uso único, tipo um convite
3. Os comandos prontos para copiar

> **Guarde a tela aberta**, você vai copiar dali. Um token serve para **um
> computador só** e vale **2 horas**. Precisa de outra máquina? Rode o comando
> de novo (sem o `-Publicar`, que só é necessário na primeira vez).

## 3.2 Levar para o outro computador

Copie a pasta **`dist\agente`** para o outro computador — pen drive, rede,
como preferir.

> O outro computador **não precisa instalar nada**. Nem .NET, nem banco de
> dados. Só a pasta.

## 3.3 No outro computador: ligar o agente

1. Abra a pasta `agente` que você copiou
2. Clique na barra de endereço, digite `powershell`, **Enter**
3. Cole as **três linhas** que a tela do passo 3.1 mostrou. Elas se parecem
   com isto (os números serão diferentes no seu caso):

```
$env:Agent__ServerUrl='http://192.168.15.9:5020'
```
```
$env:Agent__EnrollmentToken='cole-aqui-o-token-que-apareceu'
```
```
.\informE.Agent.Worker.exe
```

Em alguns segundos esse computador aparece na tela do informE, no menu
**Equipamentos**.

**Deixe essa janela aberta** enquanto quiser monitorar. Fechar a janela
desliga o agente.

---

# Parte 4 — Usando

## Ver os computadores

Menu **Equipamentos**. Aparece uma lista com uso de processador, memória,
disco e há quanto tempo cada máquina está ligada.

Clique numa linha para ver os detalhes daquele computador.

> Você vai ver **105 computadores de exemplo** junto com os reais. Eles existem
> para as telas não ficarem vazias em demonstrações. Os reais aparecem no meio
> deles, com dados verdadeiros.

## Mandar um comando

Menu **Execuções** → botão **+ Nova Execução**:

1. Escolha a ação
2. Escolha o computador
3. Clique em **Executar**

O resultado aparece na lista em alguns segundos.

**Comece por "Informações do Sistema"** — ela só *lê* dados, não muda nada na
máquina. É a mais segura para experimentar.

As outras ações:

| Ação | O que faz |
|---|---|
| Informações do Sistema | Mostra os dados da máquina. Não altera nada |
| Limpeza de Disco | Apaga arquivos temporários. **Não** toca em documentos |
| Atualização WinGet | Atualiza os programas instalados |
| Atualização do Windows | Procura atualizações do Windows |
| Diagnóstico de Rede | Testa a conexão |
| Reinicialização | **Reinicia** o computador |
| Desligamento | **Desliga** o computador |

> As duas últimas fazem exatamente o que dizem, na hora. Cuidado ao escolher.

---

# Parte 5 — Desligar

Na janela preta do computador principal:

```
powershell -ExecutionPolicy Bypass -File informe.ps1 -Parar
```

Nos computadores monitorados, basta fechar a janela do agente.

---

# Quando algo não funciona

## "Não consigo entrar"

O e-mail é `admin@cps.sp.gov.br` — com **cps**, não "etec". A senha é
`informe123`, tudo junto e minúsculo.

Se aparecer *"Esta conta possui acesso de Administrador"*, você está na tela
errada: volte e escolha **Administrador** em vez de Viewer.

## Apareceu um monte de erro vermelho falando de "arquivo bloqueado"

O informE já estava aberto. Rode:

```
powershell -ExecutionPolicy Bypass -File informe.ps1 -Parar
```

E depois ligue de novo. (As versões novas do script já resolvem isso sozinhas.)

## "O outro computador não aparece"

Confira nesta ordem:

1. **O convite venceu?** O token vale 2 horas e serve para uma máquina só.
   Gere outro com `novo-agente.ps1`.
2. **O endereço está certo?** Tem que ser o número que apareceu na tela do
   computador principal, e a porta é **5020**.
3. **O firewall?** No computador monitorado, cole isto — deve responder
   `informE.Server online`:

```
curl http://192.168.15.9:5020/
```

   (troque pelo endereço que apareceu na sua tela). Se não responder nada, o
   firewall do computador principal está bloqueando: ligue o informE lá de novo
   e clique em **Permitir acesso**.

4. **Esse computador já foi monitorado antes?** Ele guarda o registro antigo e
   o servidor recusa. Apague o registro colando isto na máquina monitorada:

```
Remove-Item "$env:LOCALAPPDATA\informE\*.identity"
```

## "Quero começar tudo de novo, do zero"

Cole as quatro linhas, uma por vez, no computador principal:

```
powershell -ExecutionPolicy Bypass -File informe.ps1 -Parar
```
```
Remove-Item "$env:USERPROFILE\informe-pgdata" -Recurse -Force
```
```
Remove-Item "$env:LOCALAPPDATA\informE\*.identity" -ErrorAction SilentlyContinue
```
```
powershell -ExecutionPolicy Bypass -File informe.ps1
```

---

# Duas coisas que é bom saber

**Depois de reiniciar o computador, ligue o informE de novo.** Ele não sobe
sozinho junto com o Windows — rode o comando da Parte 2.2. Ele percebe o que já
existe e só liga o que falta, então é rápido.

**O agente também não volta sozinho** depois de reiniciar a máquina monitorada.
Abra a pasta e rode de novo.

> Fazer os dois subirem junto com o Windows está planejado, mas ainda não está
> pronto — veja `docs/plano-outubro-novembro.md`.

---

## Para quem é técnico

Detalhes de porta, banco, escopo por papel, resolução de problemas do
PostgreSQL e as armadilhas do PowerShell 5.1 estão em
**`docs/ambiente-banco.md`**. A documentação da API roda em
`https://localhost:5021/scalar/v1` com o servidor no ar.
