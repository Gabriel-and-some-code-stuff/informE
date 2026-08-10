namespace informE.Domain.Entities;

// Software instalado — inventário por device (M-N com Devices via tabela de junção).
public class Software
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public DateTimeOffset DetectedAt { get; set; } // quando o agente coletou pela primeira vez

    public ICollection<Device> Devices { get; set; } = [];

    public Software() { }

    // Construtor padrão
    public Software(string name, string? version)
    {
        if (ValidateName(name))
            Name = name;

        if (ValidateVersion(version))
            Version = version;

        DetectedAt = DateTimeOffset.Now;
    }

    // Métodos de validação
    private static bool ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do software não pode ser vazio.");

        if (name.Length > 100)
            throw new ArgumentException("O nome do software ultrapassou o limite de caracteres.");

        return true;
    }

    private static bool ValidateVersion(string? version)
    {
        if (version != null && version.Length > 50)
            throw new ArgumentException("A versão do software ultrapassou o limite de caracteres.");

        return true;
    }

    // Métodos de domínio
    public void UpdateName(string name)
    {
        if (ValidateName(name))
            Name = name;
    }

    public void UpdateVersion(string? version)
    {
        if (ValidateVersion(version))
            Version = version;
    }
}
