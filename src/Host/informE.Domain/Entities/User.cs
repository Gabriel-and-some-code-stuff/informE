using informE.Domain.Enums;
using System.Text.RegularExpressions;

namespace informE.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // Argon2id via IPasswordHasher
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Session> Sessions { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];

    public User() { }

    // Construtor padrão

    public User(string username, string email, string passwordHash, UserRole role)
    {
        if (ValidateUsername(username))
        {
            Username = username;
        }
        if (ValidateEmail(email))
        {
            Email = email;
        }

        Role = role;
        PasswordHash = passwordHash;
        CreatedAt = DateTimeOffset.Now;
    }

    // Criação dos métodos

    public void UpdateUsername(string username)
    {
        if (ValidateUsername(username))
        {
            Username = username;
        }
    }

    public static bool ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("O nome de usuário não pode ser vazio.");

        if (username.Length > 60)
            throw new ArgumentException("O nome de usuário ultrapassou o limite de caracteres.");

        if (!username.All(char.IsLetterOrDigit))
            throw new ArgumentException("O nome de usuário contém caracteres inválidos.");

        return true;
    }

    //Validação do Email

    public static bool ValidateEmail(string email)
    {
        string regex = @"^[^@\s]+@[^@\s]+\.(com|net|org|gov|br|edu)$";

        return Regex.IsMatch(email, regex, RegexOptions.IgnoreCase);
    }


    public void UpdateEmail(string email)
    {
        if (ValidateEmail(email))
        {
            Email = email;
        }
    }
}
