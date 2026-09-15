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

        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri("https://localhost:5021/")
        });
        builder.Services.AddSingleton<InformEApiClient>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
