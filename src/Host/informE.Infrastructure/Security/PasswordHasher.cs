using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using informE.Application.Interfaces;

namespace informE.Infrastructure.Security;

// Formato do hash gerado bate com o regex de validacao em Device.cs:
// $argon2id$v=19$m=<memKb>,t=<iter>,p=<parallelism>$<saltBase64>$<hashBase64>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 4;
    private const int MemorySizeKb = 65536; // 64 MB
    private const int DegreeOfParallelism = 2;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = ComputeHash(password, salt, Iterations, MemorySizeKb, DegreeOfParallelism);

        return $"$argon2id$v=19$m={MemorySizeKb},t={Iterations},p={DegreeOfParallelism}" +
               $"${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        // "$argon2id$v=19$m=...,t=...,p=...$<salt>$<hash>" -> 5 segmentos com RemoveEmptyEntries
        var parts = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            return false;

        var parameters = parts[2].Split(',');
        var memoryKb = int.Parse(parameters[0]["m=".Length..]);
        var iterations = int.Parse(parameters[1]["t=".Length..]);
        var parallelism = int.Parse(parameters[2]["p=".Length..]);

        var salt = Convert.FromBase64String(parts[3]);
        var expectedHash = Convert.FromBase64String(parts[4]);

        var computedHash = ComputeHash(password, salt, iterations, memoryKb, parallelism);

        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    private static byte[] ComputeHash(string password, byte[] salt, int iterations, int memoryKb, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            Iterations = iterations,
            MemorySize = memoryKb,
        };

        return argon2.GetBytes(HashSizeBytes);
    }
}
