using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace informE.Agent.Worker;

// A identidade permanente da máquina: deviceId + chave do agente.
//
// A chave é o equivalente a uma senha — quem a tiver consegue se passar por esta
// máquina no AgentHub. Por isso vai para o disco protegida por DPAPI
// (ARCHITECTURE.md §4): o Windows cifra usando material derivado da própria
// máquina, então copiar o arquivo para outro computador não adianta nada.
public record AgentIdentity(Guid DeviceId, string AgentKey);

public class AgentIdentityStore(IOptions<AgentOptions> options, ILogger<AgentIdentityStore> logger)
{
    private readonly AgentOptions _options = options.Value;

    // Entropia adicional do DPAPI: mesmo que alguém consiga rodar código como o
    // mesmo usuário, precisa conhecer este valor para decifrar.
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("informE.Agent.v1");

    private string Caminho => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "informE",
        _options.IdentityFileName);

    public AgentIdentity? Carregar()
    {
        if (!File.Exists(Caminho))
            return null;

        try
        {
            var protegido = File.ReadAllBytes(Caminho);
            var json = Desproteger(protegido);

            return JsonSerializer.Deserialize<AgentIdentity>(json);
        }
        catch (Exception ex)
        {
            // Arquivo corrompido, ou copiado de outra máquina (o DPAPI não decifra).
            // Devolver null faz o agente refazer o enroll em vez de morrer no boot.
            logger.LogWarning(ex, "Identidade em {Caminho} ilegível. Será feito um novo registro.", Caminho);
            return null;
        }
    }

    public void Salvar(AgentIdentity identidade)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Caminho)!);

        var json = JsonSerializer.Serialize(identidade);
        File.WriteAllBytes(Caminho, Proteger(json));

        logger.LogInformation("Identidade salva em {Caminho}.", Caminho);
    }

    // O guard OperatingSystem.IsWindows() não é decorativo: além de o analisador
    // exigir (CA1416), o CI builda esta solução no ubuntu. Fora do Windows o
    // arquivo fica em texto claro — aceitável só porque o agente é Windows-only
    // em produção e isso existe para o build/CI não quebrar.
    private static byte[] Proteger(string json)
    {
        var bruto = Encoding.UTF8.GetBytes(json);

        return OperatingSystem.IsWindows()
            ? ProtectedData.Protect(bruto, Entropia, DataProtectionScope.LocalMachine)
            : bruto;
    }

    private static string Desproteger(byte[] protegido)
    {
        var bruto = OperatingSystem.IsWindows()
            ? ProtectedData.Unprotect(protegido, Entropia, DataProtectionScope.LocalMachine)
            : protegido;

        return Encoding.UTF8.GetString(bruto);
    }
}
