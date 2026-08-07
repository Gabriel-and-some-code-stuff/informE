using informE.Domain.Entities;

namespace informE.Domain.Tests;

// Placeholder — mesmo padrão, entidade diferente.
public class GroupTests
{
    [Fact]
    public void UpdateGroupName_DeveAtualizarONome()
    {
        // Arrange

        var date = DateTime.Now;

        var group = new Group("Lab 1", "Laboratório principal", Guid.Empty);

        // Act
        group.UpdateGroupName("Lab 1 - Térreo");

        // Assert
        Assert.Equal("Lab 1 - Térreo", group.Name);

        var dateAfter = DateTime.Now;

    }

    // TODO: quando o construtor receber ownerId, testar que group.OwnerId != Guid.Empty.
}
