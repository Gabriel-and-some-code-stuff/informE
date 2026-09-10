namespace informE.Desktop;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Título "informE", não "informE.Desktop" — o nome do assembly não
		// interessa a quem usa o sistema.
		var janela = new Window(new MainPage()) { Title = "informE" };

#if WINDOWS
		// TELA CHEIA de verdade: sem barra de título e sem barra de tarefas.
		//
		// A versão anterior chamava Maximize(), que só ocupa a área de trabalho
		// — a janela continuava em modo janela, com barra de título em cima e a
		// barra do Windows aparecendo embaixo. Numa gravação isso enquadra o
		// desktop junto com o produto.
		//
		// FullScreen é outro presenter, não um estado do OverlappedPresenter.
		//
		// Sai com F11 (ver MainPage). Sem uma saída, a janela não teria nem
		// botão de fechar — só Alt+F4.
		janela.HandlerChanged += (_, _) => AplicarJanela(janela);
#endif

		return janela;
	}

#if WINDOWS
	// Guardada para o F11 poder alternar sem procurar a janela de novo.
	internal static Microsoft.UI.Windowing.AppWindow? JanelaNativa { get; private set; }

	private static void AplicarJanela(Window janela)
	{
		if (janela.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativa)
			return;

		var handle = WinRT.Interop.WindowNative.GetWindowHandle(nativa);
		var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
		var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);

		if (appWindow is null)
			return;

		JanelaNativa = appWindow;

		// Ícone da BARRA DE TAREFAS e do alt-tab.
		//
		// O ícone embutido no .exe não basta: o WinUI 3 usa o ícone que a
		// AppWindow declara, e sem esta chamada ele fica com o padrão do .NET —
		// era o logo roxo que aparecia na barra de tarefas.
		//
		// appicon.ico é gerado pelo MauiIcon (resizetizer) e vai para a pasta de
		// saída junto do executável.
		var icone = Path.Combine(AppContext.BaseDirectory, "appicon.ico");

		if (File.Exists(icone))
			appWindow.SetIcon(icone);

		appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
	}

	// F11 alterna tela cheia, como em qualquer navegador.
	//
	// Chamado do JavaScript porque o teclado, num BlazorWebView, chega no
	// conteudo web -- a janela nativa nunca ve a tecla. JSInvokable e o caminho
	// de volta.
	[Microsoft.JSInterop.JSInvokable]
	public static void AlternarTelaCheia()
	{
		if (JanelaNativa is null)
			return;

		var cheia = JanelaNativa.Presenter.Kind
			== Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen;

		JanelaNativa.SetPresenter(cheia
			? Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped
			: Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
	}
#endif
}
