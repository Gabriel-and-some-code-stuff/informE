using System.Net;
using System.Net.Mail;
using informE.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace informE.Infrastructure.Email;

public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
            throw new InvalidOperationException(
                "SMTP não configurado (seção 'Smtp' do appsettings). Sem isso, redefinição de senha por e-mail não funciona.");

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        // Servidor de escola às vezes é relay aberto na LAN, sem credencial.
        if (!string.IsNullOrWhiteSpace(_options.Username))
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false, // texto puro: menos superfície e passa melhor por filtro de spam
        };
        message.To.Add(to);

        await client.SendMailAsync(message, ct);
    }
}
