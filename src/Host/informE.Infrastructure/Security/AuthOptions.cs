namespace informE.Infrastructure.Security;

// Populado via appsettings.json, seção "Auth". Ver DependencyInjection.cs.
public class AuthOptions
{
    public const string SectionName = "Auth";

    // Usado quando a seção NÃO existe na configuração. Uma regra de quem pode ter
    // conta não pode falhar aberta: sem este padrão, um appsettings sem a seção
    // "Auth" aceitaria qualquer e-mail do mundo.
    //
    // O DoD pede cps.sp.gov.br; etec entra junto porque é o domínio da massa de
    // desenvolvimento (SeedData) e do enroll-agent.ps1. Em produção,
    // appsettings.Production.json restringe a lista a cps.sp.gov.br.
    public static readonly string[] PadraoInstitucional = ["cps.sp.gov.br", "cps.sp.gov.br"];

    // ⚠️ Inicializa VAZIO de propósito. O binder de configuração do .NET
    // ACRESCENTA a arrays que já vêm preenchidos pelo inicializador em vez de
    // substituí-los — com o padrão aqui, a lista lida do appsettings saía
    // duplicada ("cps, etec, cps, etec"), o que aparecia na mensagem de erro
    // mostrada ao usuário. O padrão vive em PadraoInstitucional e é aplicado no
    // DependencyInjection, que consegue distinguir "seção ausente" de "lista
    // explicitamente vazia" (esta última desliga a restrição de propósito).
    public string[] DominiosPermitidos { get; set; } = [];
}
