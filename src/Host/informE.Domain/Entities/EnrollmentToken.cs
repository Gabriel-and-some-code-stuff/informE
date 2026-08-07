namespace informE.Domain.Entities;

// Token de uso único gerado pelo admin para registrar um novo agente.
public class EnrollmentToken
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty; // valor apresentado ao agente
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsUsed { get; set; }

    public Guid CreatedByUserId { get; set; }
    public Guid? RedeemedByDeviceId { get; set; } // preenchido quando o agente faz enroll

    public EnrollmentToken() { }

    // Construtor para registro padrão
    public EnrollmentToken(string token, bool isUsed, Guid createdByUserId, Guid? redeemedByDeviceId)
    {
        Token = token;
        ExpiresAt = (DateTimeOffset.Now).AddHours(2);
        IsUsed = isUsed;
        CreatedByUserId = createdByUserId;
        RedeemedByDeviceId = redeemedByDeviceId;
    }
}
