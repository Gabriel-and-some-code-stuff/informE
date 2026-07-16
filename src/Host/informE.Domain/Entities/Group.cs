namespace informE.Domain.Entities;

public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public Guid OwnerId { get; set; } // User que criou o grupo
    public ICollection<Device> Devices { get; set; } = [];
}
