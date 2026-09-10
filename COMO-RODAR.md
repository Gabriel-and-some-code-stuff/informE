# Como rodar o informE

Testado em máquina Windows 11 limpa. Do zero ao sistema no ar: **~5 minutos**,
sendo quase todo esse tempo instalação do PostgreSQL.

---

## Máquina principal (a que roda o servidor)

### O que precisa ter antes

**Só o .NET 10.** Se não tiver:

```powershell
winget install Microsoft.DotNet.SDK.10
```

Feche e reabra o terminal depois de instalar.

> O PostgreSQL o script instala sozinho se faltar. **Docker não é usado.**

### Rodar

```powershell
git clone https://github.com/Gabriel-and-some-code-stuff/informE.git
cd informE
powershell -ExecutionPolicy Bypass -File informe.ps1
```

É isso. O script:

1. confere .NET e PostgreSQL (instala o Postgres se faltar)
2. cria e sobe um banco em `%USERPROFILE%\informe-pgdata`
3. compila (~6s)
4. sobe o servidor, que **aplica as migrations e popula o banco sozinho**
5. abre o aplicativo
6. imprime o endereço que as outras máquinas devem usar

**Login:** `admin@cps.sp.gov.br` · senha `informe123`

### Na primeira vez

O Windows vai perguntar se libera o `dotnet` no firewall. **Aceite, para rede
privada** — sem isso as outras máquinas não alcançam o servidor.

### Variações

```powershell
.\informe.ps1 -SemApp     # só servidor, não abre o aplicativo
.\informe.ps1 -SoBanco    # só o banco
.\informe.ps1 -Parar      # derruba tudo
```

---

## Outras máquinas (as monitoradas)

**Essas não precisam de .NET, nem de banco, nem de nada.** Só o executável.

### Na máquina principal

```powershell
.\novo-agente.ps1 -Publicar
```

Isso gera duas coisas:

- a pasta **`dist\agente`** com o executável (74 MB, tudo dentro)
- um **token de registro**, e o comando pronto para colar

O `-Publicar` só é necessário na primeira vez. Depois, `.\novo-agente.ps1`
sozinho já gera um token novo.

> **Um token por máquina.** Vale 2 horas e é de uso único — rode o
> `novo-agente.ps1` uma vez para cada computador.

### Na máquina monitorada

Copie a pasta `dist\agente` (pen drive, rede, o que for) e rode lá:

```powershell
$env:Agent__ServerUrl='http://192.168.15.9:5020'
$env:Agent__EnrollmentToken='<o token que o script imprimiu>'
.\informE.Agent.Worker.exe
```

Troque o IP pelo que o `informe.ps1` imprimiu na máquina principal.

Em poucos segundos a máquina aparece na tela de **Equipamentos**, com CPU, RAM,
disco e tempo ligado reais.

> **Porta 5020 (http), não 5021 (https).** O certificado de desenvolvimento vale
> só para `localhost`; uma máquina remota batendo em `https://<ip>` falha na
> validação do nome.

### Várias máquinas de teste no mesmo computador

Útil para ensaiar sem VM. Cada agente precisa de nome, MAC e arquivo de
identidade próprios — senão o segundo tenta reusar o registro do primeiro:

```powershell
$env:Agent__HostnameOverride='PC-LAB-01'
$env:Agent__IdentityFileName='pc-lab-01.identity'
$env:Agent__MacAddressOverride='AA:BB:CC:E1:00:01'
.\informE.Agent.Worker.exe
```

---

## Usando

1. **Login** — `admin@cps.sp.gov.br` / `informe123`
2. **Equipamentos** — as máquinas reais aparecem junto com 105 de exemplo.
   Filtre por laboratório, conexão ou nome; clique numa linha para ver detalhe
   e hardware
3. **Execuções → Nova Execução** — escolha a ação, escolha as máquinas,
   Executar. O resultado volta em segundos, com a saída real
4. Ações disponíveis: Informações do Sistema, Limpeza de Disco, Atualização
   WinGet, Atualização do Windows, Reinicialização, Desligamento, Diagnóstico
   de Rede

**Para demonstrar, comece por "Informações do Sistema"** — é leitura pura, não
muda nada na máquina.

---

## Quando algo não funciona

### A máquina remota não aparece

Nesta ordem:

1. **Firewall.** Na máquina principal, teste do outro computador:
   `curl http://<ip-do-servidor>:5020/` — deve responder
   `informE.Server online`. Se não responder, o firewall está barrando.
2. **Endereço errado.** Confirme que o `Agent__ServerUrl` usa a porta **5020**
   e **http**, não 5021/https.
3. **Token gasto.** É de uso único e vale 2 horas. Gere outro.
4. **Identidade antiga.** Se a máquina já foi registrada antes e o banco foi
   recriado, o agente tenta reusar o registro velho e o servidor recusa:

   ```powershell
   Get-ChildItem "$env:LOCALAPPDATA\informE\*.identity" | Rename-Item -NewName { $_.Name + '.velha' }
   ```

### O banco não sobe

```powershell
Get-Content "$env:USERPROFILE\informe-pgdata\server.log" -Tail 30
```

Se a porta 5432 estiver ocupada por outro PostgreSQL, pare o serviço dele ou
mude a porta no topo do `start-local.ps1` (e na connection string do
`appsettings.json`).

### Recomeçar do zero

```powershell
.\informe.ps1 -Parar
Remove-Item "$env:USERPROFILE\informe-pgdata" -Recurse -Force
Get-ChildItem "$env:LOCALAPPDATA\informE\*.identity" | Remove-Item
.\informe.ps1
```

---

## O que fica de pé depois de fechar o terminal

| Componente | Sobrevive? |
|---|---|
| Banco | sim, até reiniciar o Windows — não é serviço |
| Servidor | não, morre com o terminal |
| Agente | não, ainda é aplicação de console |

Depois de reiniciar a máquina, rode o `informe.ps1` de novo. Ele é idempotente:
detecta o que já existe e só sobe o que falta.

Virar serviço do Windows é uma linha (`AddWindowsService()`) e está planejado —
ver `docs/plano-outubro-novembro.md`.

---

## Detalhes que valem saber

**Duas portas:** `5021` https para o aplicativo (mesma máquina, confia no
certificado de desenvolvimento) e `5020` http para os agentes remotos.

**O servidor migra e semeia no boot.** Não existe passo manual de banco.

**O seed traz 105 máquinas de exemplo** para as telas não ficarem vazias. As
máquinas reais aparecem junto, com dados de verdade.

**Documentação da API:** `https://localhost:5021/scalar/v1` — dá para testar
qualquer endpoint pelo navegador.
