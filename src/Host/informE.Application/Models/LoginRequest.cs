namespace informE.Application.Models;

// DeviceLabel e IpAddress vêm do request HTTP (User-Agent e IP remoto), não de
// um campo que o usuário digita.
public record LoginRequest(
    string Email,
    string Password,
    string IpAddress,
    string? DeviceLabel
);
