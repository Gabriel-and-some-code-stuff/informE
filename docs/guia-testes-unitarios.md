# Guia de Testes Unitarios — informE

> Documento de referencia da equipe. Ultima atualizacao: agosto/2026.

---

## O que e um teste unitario

Um teste unitario verifica **um unico comportamento** de uma unica classe, sem banco de dados, sem rede, sem nada externo. No Domain do informE nao ha nenhuma dependencia externa — tudo e C# puro — entao cada entidade e trivial de testar: instancia, chama o metodo, confere o resultado.

---

## As seis regras fundamentais

| # | Regra | O que significa |
|---|-------|----------------|
| 1 | **Um teste = um comportamento** | Nao teste tres coisas no mesmo `[Fact]`. Se falhar, voce nao sabe qual das tres quebrou. |
| 2 | **Sem logica no teste** | Nenhum `if`, nenhum `for`. Se precisar de logica, use `[Theory]` com `[InlineData]`. |
| 3 | **O nome documenta o comportamento** | Padrao: `MetodoTestado_Cenario_ResultadoEsperado`. Quem le o nome deve entender o teste sem abrir o codigo. |
| 4 | **Arrange → Act → Assert** | Prepare os dados, execute a acao, verifique o resultado. Sempre nessa ordem. |
| 5 | **Teste o comportamento, nao a implementacao** | Nao teste o estado interno de uma lista privada. Teste o que o metodo retorna ou como muda o estado publico. |
| 6 | **Testes devem ser deterministicos** | Um teste que passa as vezes e falha outras e pior que nenhum teste. Nao dependa de horario, aleatoriedade ou ordem de execucao. |

---

## Anatomia de um teste bem feito

```csharp
[Fact]
public void UpdateUsername_ComNomeValido_AtualizaOUsername()
{
    // Arrange — prepara o estado inicial
    var user = new User("gabriel", "gabriel@etec.sp.gov.br", "hash", UserRole.Admin);

    // Act — executa o comportamento sendo testado
    user.UpdateUsername("gabrielv2");

    // Assert — verifica o resultado. Um Assert por Fact.
    Assert.Equal("gabrielv2", user.Username);
}
```

Os comentarios `// Arrange / Act / Assert` sao opcionais depois que o padrao virar reflexo, mas ajudam enquanto a equipe esta aprendendo.

---

## `[Fact]` vs `[Theory]`

### `[Fact]` — um unico cenario

Use quando o comportamento so precisa ser verificado uma vez, com valores fixos.

```csharp
[Fact]
public void Construtor_DeveIniciarComoAtiva()
{
    var session = new Session("192.168.0.10", DateTimeOffset.Now, "hash", Guid.NewGuid());

    Assert.True(session.IsActive);
}
```

### `[Theory]` + `[InlineData]` — multiplos cenarios, mesma logica

Use quando voce quer testar o mesmo comportamento com entradas diferentes. O xUnit roda o metodo uma vez por `[InlineData]`.

```csharp
[Theory]
[InlineData("gabriel123", true)]   // valido
[InlineData("", false)]            // vazio — invalido
[InlineData("com espaco", false)]  // espaco — invalido
[InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)] // > 60 chars
public void ValidateUsername_AceitaOuRejeita(string username, bool devePassar)
{
    if (devePassar)
        Assert.True(User.ValidateUsername(username));
    else
        Assert.Throws<ArgumentException>(() => User.ValidateUsername(username));
}
```

Cada `[InlineData]` aparece como um teste separado no runner — se um falhar, os outros ainda rodam.

---

## Testando excecoes com `Assert.Throws`

Quando um metodo deve lancar excecao em entradas invalidas, use `Assert.Throws`. Nao use try/catch nos testes.

```csharp
// ERRADO — esconde a excecao, mascara falhas
try {
    User.ValidateUsername("");
    Assert.Fail("devia ter lancado");
} catch (ArgumentException) { }

// CERTO — limpo e idiomatico
Assert.Throws<ArgumentException>(() => User.ValidateUsername(""));
```

---

## Testando timestamps com `Assert.InRange`

Nunca compare `DateTimeOffset` com `Assert.Equal` — vai falhar por diferenca de microssegundos. Capture o intervalo antes e depois da criacao.

```csharp
[Fact]
public void Construtor_LoginAtDeveSerHoraAtual()
{
    var antes = DateTimeOffset.Now;

    var session = new Session("192.168.0.10", DateTimeOffset.Now, "hash", Guid.NewGuid());

    var depois = DateTimeOffset.Now;

    // LoginAt deve estar dentro da janela de execucao do teste
    Assert.InRange(session.LoginAt, antes, depois);
}
```

---

## O que nao vale a pena testar

| Nao testar | Por que |
|------------|---------|
| Getters e setters automaticos `{ get; set; }` | Sao do compilador, nao ha logica de negocio |
| Construtores sem validacao | Se so atribui propriedades, nao ha o que testar |
| Codigo do EF Core / banco | Isso e teste de integracao, nao unitario |
| Bibliotecas de terceiros (Argon2, Regex da stdlib) | Confie na lib |

| Testar | Por que |
|--------|---------|
| Validacoes com regras de negocio | Tamanho maximo, formato, valores proibidos |
| Mutacoes de estado | Metodos que alteram propriedades da entidade |
| Casos de borda | String vazia, `Guid.Empty`, data no passado |
| Excecoes esperadas | Entradas que devem lancar `ArgumentException` |

---

## Erros encontrados nos testes — agosto/2026

### 1. Import nao usado

O `UserTests.cs` importava `System.Security.Cryptography.X509Certificates` e `Xunit.Abstractions` sem usar nenhum dos dois. Imports desnecessarios sao ruido — o compilador avisa, remova sempre.

### 2. Teste que nao compila apos mudanca de assinatura

O construtor de `Session` foi alterado para remover `expiresAt` (agora calculado internamente), mas os testes continuaram passando o parametro.

```csharp
// Quebrado — expiresAt nao existe mais no construtor
var session = new Session(expiresAt: DateTimeOffset.Now.AddDays(7), ...);

// Correto
var session = new Session("192.168.0.10", DateTimeOffset.Now, "hash", userId);
```

**Regra:** quando voce altera a assinatura de um construtor ou metodo, rode `dotnet test` imediatamente. Esse erro fica oculto ate o CI rodar.

### 3. Variaveis mortas no teste

O `GroupTests.cs` declarava `var date = DateTime.Now` e `var dateAfter = DateTime.Now` mas nunca as usava. Variavel declarada e nao usada = Assert esquecido ou codigo morto. Delete.

### 4. Arquivo de teste vazio com nome errado

O `AuditLogTests.cs` continha `internal class DatetimeTests` sem nenhum teste. Arquivo sem teste nao gera falha no CI, mas polui o projeto. Se nao ha o que testar ainda, deixe um placeholder com `// TODO`.

---

## Proximos testes a escrever

Cada `// TODO` nos arquivos corresponde a um metodo que ainda nao existe no Domain. Quando o metodo for implementado, o teste correspondente deve entrar na **mesma PR**.

- [ ] `Session.Revoke()` → `IsActive` deve virar `false`
- [ ] `Session.IsExpired()` → com `ExpiresAt` no passado deve retornar `true`
- [ ] `Device.MarkOnline()` / `MarkOffline()` → `Status` deve mudar
- [ ] `Group.Deactivate()` → `IsActive` deve virar `false`
- [ ] `EnrollmentToken.Redeem()` → `IsUsed` deve virar `true`, nao deve permitir reuso

---

## Rodando os testes

```bash
# Todos os testes do Domain
dotnet test tests/informE.Domain.Tests

# So uma classe
dotnet test tests/informE.Domain.Tests --filter SessionTests

# So um metodo
dotnet test tests/informE.Domain.Tests --filter "Construtor_DeveIniciarComoAtiva"
```
