using informE.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace informE.Infrastructure.Email;

// IEmailSender para quando NAO ha SMTP configurado.
//
// POR QUE EXISTE: o SmtpEmailSender lanca quando `Smtp:Host` esta vazio, e isso
// derrubava o /auth/forgot-password inteiro com 409 -- a redefinicao de senha
// simplesmente nao funcionava em nenhuma maquina de desenvolvimento, e nao dava
// para demonstrar o fluxo.
//
// Aqui a mensagem vai para arquivo e para o log. Nao e "fingir que enviou": o
// arquivo existe, tem o conteudo exato que iria por e-mail, e o log diz onde
// esta. Quem quiser SMTP de verdade preenche a secao Smtp e o
// SmtpEmailSender volta a ser usado (ver DependencyInjection).
public class EmailParaArquivoSender(ILogger<EmailParaArquivoSender> logger) : IEmailSender
{
    private static readonly string Pasta =
        Path.Combine(Path.GetTempPath(), "informe-emails");

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Pasta);

        // Nome com data/hora e destinatario: da para achar o ultimo sem abrir
        // todos, e dois pedidos seguidos nao sobrescrevem um ao outro.
        var arquivo = Path.Combine(
            Pasta,
            $"{DateTime.Now:yyyyMMdd-HHmmss}-{to.Replace('@', '_')}.txt");

        var conteudo = $"""
            Para....: {to}
            Assunto.: {subject}
            Quando..: {DateTimeOffset.Now:dd/MM/yyyy HH:mm:ss}

            {body}
            """;

        await File.WriteAllTextAsync(arquivo, conteudo, ct);

        // O corpo VAI para o log de proposito: em desenvolvimento este e o
        // caminho mais curto para pegar o link de redefinicao. Em producao esta
        // implementacao nao e registrada.
        logger.LogWarning(
            "SMTP não configurado — e-mail para {Destinatario} gravado em {Arquivo}.\n{Corpo}",
            to, arquivo, body);
    }
}
