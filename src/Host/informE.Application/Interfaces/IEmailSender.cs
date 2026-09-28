namespace informE.Application.Interfaces;

// Port de envio de e-mail. Implementado em Infrastructure via SMTP do cliente
// (a rede da escola/empresa), porque informE é on-premise e não fala com nuvem.
//
// Deliberadamente burro: recebe destinatário, assunto e corpo prontos. Quem monta
// a mensagem é o use case — assim o texto do e-mail fica testável sem SMTP.
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
