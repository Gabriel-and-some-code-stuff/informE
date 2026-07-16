## O que muda
<!-- Uma frase descrevendo o que este PR entrega. -->

## Como testar
<!-- Passos mínimos para verificar que funciona. -->
- [ ] `docker compose up -d` → Postgres no ar
- [ ] `dotnet build informE.Host.slnx` sem erros
- [ ] (descreva o fluxo específico desta mudança)

## Checklist
- [ ] Nenhuma referência de Infrastructure/EF dentro de `Domain` ou `Application`
- [ ] IDs são `Guid`, nunca `int`
- [ ] Datas em UTC (`DateTimeOffset` ou `DateTime.UtcNow`)
- [ ] Senhas passam por `IPasswordHasher` (nunca Argon2 direto fora da Infrastructure)
- [ ] Sem `Console.WriteLine` ou `Debug.Print` esquecido
