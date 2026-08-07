using System.ComponentModel.DataAnnotations;

namespace informE.Domain.Entities;

public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public Guid OwnerId { get; set; } // User que criou o grupo
    public ICollection<Device> Devices { get; set; } = []; //

    public Group() { }

    // Construtor para registro padrão
    public Group(string name, string? description, Guid ownerId)
    {
        if (ValidateName(name))
            Name = name;

        Description = description;
        IsActive = true;
        CreatedAt = DateTimeOffset.Now;
        OwnerId = ownerId;
    }

    //Métodos de validação
    public static bool ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("O nome do grupo não pode ser vazio.");

        if (name.Length > 45)
            throw new ArgumentException("O nome do grupo ultrapassou o limite de caracteres.");

        return true;
    }

    public static bool ValidateDescription(string? description)
    {
        if(description != null && description.Length > 100)
            throw new ArgumentException("A descrição ultrapassou o limite de caracteres.");

        return true;
    }

    // Métodos de domínio
    public void UpdateGroupName(string name)
    {
        if (ValidateName(name))
            Name = name;
    }

    public void UpdateDescription(string? description)
    {
        if (ValidateDescription(description))
            Description = description;
    }
}
