using informE.Domain.Entities;
using informE.Domain.Enums;

namespace informE.Domain.Tests;

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



    [Fact]
    public void Construtor_CreatedAtMomentoAtual()
    {
        var antes = DateTimeOffset.Now;

        var user = new User(
            "gabriel",
            "gabriel.zemella@etec.sp.gov.br",
            "hash-fake",
            UserRole.Admin
        );

        var depois = DateTimeOffset.Now;

        Assert.InRange(user.CreatedAt, antes, depois);
    }


}


