using informE.Domain.Entities;

namespace informE.Domain.Tests;

// Placeholder — mesmo padrão, entidade diferente.
public class GroupTests
{
    [Fact]
    public void UpdateGroupName_DeveAtualizarONome()
    {
        var group = new Group("Lab 1", "Laboratório principal", Guid.NewGuid());

        group.UpdateGroupName("Lab 1 - Térreo");

        Assert.Equal("Lab 1 - Térreo", group.Name);
    }

    [Fact]
    public void Construtor_DevePreencherOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var group = new Group("Lab 2", null, ownerId);

        Assert.Equal(ownerId, group.OwnerId);
    }
}
