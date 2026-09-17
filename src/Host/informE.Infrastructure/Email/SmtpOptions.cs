namespace informE.Infrastructure.Email;

// Seção "Smtp" do appsettings. Servidor da própria rede do cliente — informE é
// on-premise, não manda e-mail por serviço de nuvem.
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "informE";

    // Base do link de redefinição, ex. "https://informe.etec.local/redefinir-senha".
    public string ResetPasswordUrl { get; set; } = string.Empty;
}
