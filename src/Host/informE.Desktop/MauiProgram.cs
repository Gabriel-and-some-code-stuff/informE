using Microsoft.Extensions.Logging;
using informE.Desktop.Services;
using ApexCharts;

namespace informE.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddApexChartsMaui();

        // Endereco do Host, em ordem de precedencia:
        //   1. variavel de ambiente INFORME_SERVER  (ex.: http://192.168.15.9:5020)
        //   2. arquivo informe-server.txt ao lado do executavel
        //   3. https://localhost:5021
        //
        // Sem isto o endereco era CONSTANTE no binario: apontar o app para outra
        // maquina exigia recompilar. Numa demonstracao com o Host em outro
        // computador, isso e a diferenca entre trocar um arquivo de texto e nao
        // conseguir rodar.
        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri(ResolverEnderecoDoHost()),

            // A rede de laboratorio e mais lenta que o loopback; o padrao de 100s
            // e alto demais para uma tela travar esperando.
            Timeout = TimeSpan.FromSeconds(30)
        });
        builder.Services.AddSingleton<InformEApiClient>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private const string EnderecoPadrao = "https://localhost:5021/";

    private static string ResolverEnderecoDoHost()
    {
        var doAmbiente = Environment.GetEnvironmentVariable("INFORME_SERVER");

        if (Aceitavel(doAmbiente, out var doAmbienteValido))
            return doAmbienteValido;

        try
        {
            // AppContext.BaseDirectory e a pasta do executavel — funciona tanto
            // no `dotnet run` quanto no app publicado.
            var arquivo = Path.Combine(AppContext.BaseDirectory, "informe-server.txt");

            if (File.Exists(arquivo) && Aceitavel(File.ReadAllText(arquivo), out var doArquivo))
                return doArquivo;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Arquivo ilegivel nao pode impedir o app de abrir: cai no padrao.
        }

        return EnderecoPadrao;
    }

    // Valida ANTES de entregar para o `new Uri(...)`.
    //
    // Sem esta checagem, um endereco malformado -- "192.168.15.9:5020" sem o
    // "http://", o erro de digitacao mais provavel de todos -- lancava
    // UriFormatException no CreateMauiApp e o aplicativo morria no boot, SEM
    // JANELA E SEM MENSAGEM. Trocar o IP na vespera e digitar errado nao pode
    // ser a diferenca entre demonstrar e nao abrir; cair no padrao e visivel,
    // porque a tela avisa que nao alcancou o servidor.
    private static bool Aceitavel(string? valor, out string endereco)
    {
        endereco = EnderecoPadrao;

        if (string.IsNullOrWhiteSpace(valor))
            return false;

        var candidato = Normalizar(valor.Trim());

        if (!Uri.TryCreate(candidato, UriKind.Absolute, out var uri))
            return false;

        // Só http/https: "informe-server.txt" com um caminho de arquivo dentro
        // (file://) produziria um BaseAddress que falha em toda chamada.
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        endereco = candidato;
        return true;
    }

    // BaseAddress exige a barra final: sem ela, "auth/login" sobrescreveria o
    // ultimo segmento do caminho em vez de ser acrescentado.
    private static string Normalizar(string url) =>
        url.EndsWith('/') ? url : url + "/";
}
