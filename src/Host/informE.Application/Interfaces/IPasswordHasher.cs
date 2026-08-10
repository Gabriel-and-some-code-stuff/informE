namespace informE.Application.Interfaces;

// Implementado em Infrastructure via Argon2id. Domain nunca hasha na mão.
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
