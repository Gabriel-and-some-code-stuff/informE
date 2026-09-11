using informE.Domain.Entities;
using informE.Domain.Enums;

namespace informE.Domain.Tests;

// Os construtores de User e Device faziam `if (Validate(x)) Prop = x;` — quando a
// validação falhava, a propriedade ficava em string.Empty e o dado inválido era
// descartado EM SILÊNCIO. Como email, hostname e mac_address são únicos e
// obrigatórios no banco, o primeiro registro ruim nascia quebrado e o segundo
// estourava violação de índice (500 sem explicação).
//
// Estes testes existem para essa regressão não voltar.
public class ValidacaoQueLancaTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sem-arroba")]
    [InlineData("dois@@arrobas.com")]
    [InlineData("sem@dominio")]
    public void User_ComEmailInvalido_DeveLancar(string email)
    {
        Assert.Throws<ArgumentException>(() =>
            new User("professor", email, "hash", UserRole.Viewer));
    }

    [Fact]
    public void UpdateEmail_ComEmailInvalido_DeveLancarEManterOAnterior()
    {
        var user = new User("professor", "prof@etec.sp.gov.br", "hash", UserRole.Viewer);

        Assert.Throws<ArgumentException>(() => user.UpdateEmail("invalido"));
        Assert.Equal("prof@etec.sp.gov.br", user.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nome-muito-longo-demais")] // acima de 15 caracteres
    [InlineData("-comeca-com-hifen")]
    [InlineData("12345")]                   // só dígitos
    [InlineData("com_underline")]           // caractere fora do permitido
    public void Device_ComHostnameInvalido_DeveLancar(string hostname)
    {
        Assert.Throws<ArgumentException>(() => NovoDevice(hostname: hostname));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AA:BB:CC:DD:EE")]     // faltando um par
    [InlineData("ZZ:BB:CC:DD:EE:FF")]  // não é hexadecimal
    public void Device_ComMacInvalido_DeveLancar(string mac)
    {
        Assert.Throws<ArgumentException>(() => NovoDevice(mac: mac));
    }

    [Theory]
    [InlineData("")]
    [InlineData("999.999.999.999")]
    [InlineData("nao-e-ip")]
    public void Device_ComIpInvalido_DeveLancar(string ip)
    {
        Assert.Throws<ArgumentException>(() => NovoDevice(ip: ip));
    }

    // A contraprova: entrada válida continua passando. Sem isto, um validador que
    // lançasse SEMPRE também faria os testes acima passarem.
    [Fact]
    public void Device_ComDadosValidos_DevePreencherAsPropriedades()
    {
        var device = NovoDevice();

        Assert.Equal("PC-01", device.Hostname);
        Assert.Equal("192.168.1.10", device.LastIp);
        Assert.Equal("AA:BB:CC:DD:EE:FF", device.MacAddress);
    }

    private static Device NovoDevice(
        string hostname = "PC-01",
        string ip = "192.168.1.10",
        string mac = "AA:BB:CC:DD:EE:FF") =>
        new(hostname, ip, mac, "Windows 11", "aluno", "hash-fake", null, null);
}
