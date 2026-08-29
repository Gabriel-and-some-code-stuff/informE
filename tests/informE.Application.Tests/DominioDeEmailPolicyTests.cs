using informE.Application;

namespace informE.Application.Tests;

// Só e-mail institucional entra no informE. A lista vem de configuração
// (Auth:DominiosPermitidos), com default cps + etec.
public class DominioDeEmailPolicyTests
{
    private static readonly string[] Institucionais = ["cps.sp.gov.br", "etec.sp.gov.br"];

    [Theory]
    [InlineData("professor@cps.sp.gov.br")]
    [InlineData("admin@etec.sp.gov.br")]
    [InlineData("MAIUSCULA@CPS.SP.GOV.BR")] // domínio não é case-sensitive
    public void Validar_ComDominioInstitucional_DeveAceitar(string email)
    {
        var policy = new DominioDeEmailPolicy(Institucionais);

        policy.Validar(email); // não lança
    }

    [Theory]
    [InlineData("pessoa@gmail.com")]
    [InlineData("pessoa@outlook.com")]
    [InlineData("pessoa@sp.gov.br")]              // domínio pai não vale
    [InlineData("pessoa@falso-cps.sp.gov.br")]    // sufixo parecido não vale
    public void Validar_ComDominioDeFora_DeveLancar(string email)
    {
        var policy = new DominioDeEmailPolicy(Institucionais);

        Assert.Throws<ArgumentException>(() => policy.Validar(email));
    }

    [Fact]
    public void Validar_ComListaVazia_DeveAceitarQualquerDominio()
    {
        // Lista vazia desliga a restrição — é o que permite uma instalação sem
        // domínio institucional definido e o que os testes de papel usam.
        var policy = new DominioDeEmailPolicy([]);

        policy.Validar("qualquer@coisa.com"); // não lança
    }

    [Fact]
    public void Validar_ComSubdominioNaoListado_DeveLancar()
    {
        // "aluno.cps.sp.gov.br" NÃO é "cps.sp.gov.br". A comparação é exata de
        // propósito: aceitar sufixo abriria a porta para qualquer subdomínio que
        // a instituição não controle.
        var policy = new DominioDeEmailPolicy(Institucionais);

        Assert.Throws<ArgumentException>(() => policy.Validar("aluno@aluno.cps.sp.gov.br"));
    }
}
