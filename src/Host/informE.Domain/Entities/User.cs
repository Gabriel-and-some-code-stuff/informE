using informE.Domain.Enums;

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
        Username = username;
        Role = role;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = DateTimeOffset.Now;
    }

    // Criação do método

    public void UpdateUsername(string username) 
    {
        if (string.IsNullOrWhiteSpace(username)
        {
            throw new ArgumentException("O nome de usuário não é válido.");
        }
        if (username.Length > 60)
        {
            throw new ArgumentException("O nome de usuário ultrapassou o limite de caracteres.");
        }
        if (username.Contains("@") || username.Contains("*"))

        Username = username;
    }

    //Validação do Email


    public void UpdateEmail(string email)
    {
        Email = email;

        if (email.Contains("@"))
        {

        }
        else
        {

        }



    }
