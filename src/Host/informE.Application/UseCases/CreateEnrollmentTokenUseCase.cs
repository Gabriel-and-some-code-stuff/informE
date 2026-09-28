using System.Security.Cryptography;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;

namespace informE.Application.UseCases;

// RF01/RF12 — o admin gera o token que o agente vai apresentar no /agent/enroll.
//
// Este caso de uso FALTAVA: EnrollDeviceUseCase validava e consumia o token, mas
// não existia caminho nenhum para emitir um. O fluxo de registro do agente estava
// quebrado no meio — nenhuma máquina conseguiria entrar no sistema.
public class CreateEnrollmentTokenUseCase(
    IEnrollmentTokenRepository tokenRepository,
    IUnitOfWork unitOfWork)
{
    private const int TamanhoEmBytes = 24;

    // Devolve a validade junto: antes o endpoint respondia
    // `DateTimeOffset.UtcNow.AddHours(2)` hardcoded, duplicando a constante que
    // vive no construtor de EnrollmentToken. Mudar a validade em um lugar fazia
    // a API mentir no outro.
    public async Task<(string Token, DateTimeOffset ExpiraEm)> ExecuteAsync(Guid criadoPorUserId, CancellationToken ct = default)
    {
        // Base64Url: sem '+', '/' e '=' — o token vai ser copiado à mão para o
        // instalador do agente, e esses caracteres atrapalham em linha de comando.
        var valor = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TamanhoEmBytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        // ponytail: o token vai em texto claro no banco, diferente da senha e do
        // refresh token. É de uso único e vive 2 horas — hashear exigiria o mesmo
        // esquema de "id + segredo" do reset de senha, e não compensa nesse prazo.
        // Se um dia o token virar longo-prazo, isto PRECISA mudar.
        var token = new EnrollmentToken(valor, criadoPorUserId);

        await tokenRepository.AddAsync(token, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return (valor, token.ExpiresAt);
    }
}
