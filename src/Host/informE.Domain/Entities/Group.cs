namespace informE.Domain.Entities;

public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public Guid OwnerId { get; set; } // User que criou o grupo
    public ICollection<Device> Devices { get; set; } = []; //

    public Group() { }

    // Construtor para registro padrão
    public Group(string name, string description)
    {
        Name = name;
        Description = description;
        IsActive = true;
        CreatedAt = DateTimeOffset.Now;
    }
}
