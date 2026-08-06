using informE.Domain.Entities;
using informE.Domain.Enums;

namespace informE.Domain.Tests;

// Placeholder — mostra o padrão Arrange-Act-Assert. Sem dependência de banco/mock:
// Domain é C# puro, então o teste também é.
public class UserTests
{
    [Fact]
    public void UpdateUsername_ComNomeValido_AtualizaOUsername()
    {
        // Arrange
        var user = new User("gabriel", "gabriel@etec.sp.gov.br", "hash-fake", UserRole.Admin);

        // Act
        user.UpdateUsername("gabrielv2");

        // Assert
        Assert.Equal("gabrielv2", user.Username);
    }

    [Theory]
    [InlineData("gabriel123", true)]
    [InlineData("", false)]
    [InlineData("nome com espaço", false)]
    public void ValidateUsername_DeveAceitarOuRejeitarConformeORegex(string username, bool esperadoValido)
    {
        if (esperadoValido)
        {
            Assert.True(User.ValidateUsername(username));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => User.ValidateUsername(username));
        }
    }
}
