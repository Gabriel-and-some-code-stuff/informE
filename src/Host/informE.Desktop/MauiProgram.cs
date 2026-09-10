using Microsoft.Extensions.Logging;
using informE.Desktop.Services;

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

        if (!string.IsNullOrWhiteSpace(doAmbiente))
            return Normalizar(doAmbiente);

        try
        {
            // AppContext.BaseDirectory e a pasta do executavel — funciona tanto
            // no `dotnet run` quanto no app publicado.
            var arquivo = Path.Combine(AppContext.BaseDirectory, "informe-server.txt");

            if (File.Exists(arquivo))
            {
                var conteudo = File.ReadAllText(arquivo).Trim();

                if (!string.IsNullOrWhiteSpace(conteudo))
                    return Normalizar(conteudo);
            }
        }
        catch (IOException)
        {
            // Arquivo ilegivel nao pode impedir o app de abrir: cai no padrao.
        }

        return EnderecoPadrao;
    }

    // BaseAddress exige a barra final: sem ela, "auth/login" sobrescreveria o
    // ultimo segmento do caminho em vez de ser acrescentado.
    private static string Normalizar(string url) =>
        url.EndsWith('/') ? url : url + "/";
}
