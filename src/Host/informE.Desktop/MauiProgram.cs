using Microsoft.Extensions.Logging;
using informE.Desktop.Services;
using ApexCharts;

namespace informE.Desktop;

public static class MauiProgram
{
    // Endereço do Server. O padrão serve a máquina que roda Desktop e Server
    // juntos (demo); num laboratório, o Desktop fica numa máquina e o Server em
    // outra, então o endereço precisa ser configurável SEM recompilar:
    //
    //   setx INFORME_SERVER_URL http://192.168.0.10:5020/
    //
    // Sempre http:// e nunca https:// em desenvolvimento: o Server só redireciona
    // para HTTPS fora de Development (ver Program.cs), e o certificado de dev é
    // emitido para "localhost" — de outra máquina ele não vale.
    private const string EnderecoPadrao = "http://localhost:5020/";

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

        builder.Services.AddSingleton(CriarHttpClient());
        builder.Services.AddSingleton<InformEApiClient>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    // O informE é on-premise: o Server está SEMPRE na mesma rede do Desktop —
    // localhost na demo, IP do laboratório na escola. Nunca do outro lado da
    // internet. Logo, nenhuma requisição daqui deve passar por proxy.
    //
    // Isso não é preferência, é correção de bug. Por padrão o HttpClient herda o
    // proxy do sistema (HttpClient.DefaultProxy → variáveis HTTP_PROXY/HTTPS_PROXY
    // ou, na falta delas, o registro do WinINET). Na rede da Etec existe proxy
    // Squid, e a chave ProxyOverride do WinINET vem VAZIA — ou seja, sem lista de
    // exceção, nem para localhost. Resultado: o POST de /auth/login saía da
    // máquina rumo ao proxy, que obviamente não alcança o loopback de quem pediu,
    // e devolvia o erro genérico dele: **503 Service Unavailable**.
    //
    // Era esse o 503 que aparecia no login mesmo com o Server no ar: ele nunca
    // vinha do Server — vinha do proxy. Desligar o proxy aqui faz o socket ir
    // direto ao destino e torna o app imune à configuração de rede da máquina,
    // que é o que "roda em qualquer lugar" exige.
    private static HttpClient CriarHttpClient()
    {
        var endereco = Environment.GetEnvironmentVariable("INFORME_SERVER_URL");

        if (string.IsNullOrWhiteSpace(endereco))
            endereco = EnderecoPadrao;

        // BaseAddress só concatena rotas relativas corretamente terminando em "/".
        if (!endereco.EndsWith('/'))
            endereco += "/";

        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            Proxy = null,

            // O HttpClient é singleton: sem isso a conexão nunca reavalia o DNS.
            // Importa quando INFORME_SERVER_URL aponta para um hostname da rede.
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(endereco),

            // O padrão são 100 s. Se o Server estiver fora, o operador fica quase
            // dois minutos olhando o spinner do login sem mensagem nenhuma.
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
