namespace informE.Desktop;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Título "informE", não "informE.Desktop".
		//
		// O nome do PROJETO estava aparecendo na barra de título e na barra de
		// tarefas do Windows — quem usa o sistema não tem por que ver o nome do
		// assembly.
		var janela = new Window(new MainPage()) { Title = "informE" };

#if WINDOWS
		// Abre MAXIMIZADA.
		//
		// A janela nascia no tamanho padrão do MAUI (~1000x800). Num painel com
		// tabela de 7 colunas isso força rolagem horizontal logo de cara, e numa
		// gravação sobra área de trabalho em volta.
		//
		// Vai no HandlerChanged porque o AppWindow do WinUI só existe depois que
		// o handler nativo é criado — tentar antes disso pega null.
		janela.HandlerChanged += (_, _) =>
		{
			if (janela.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativa)
				return;

			var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(
				WinRT.Interop.WindowNative.GetWindowHandle(nativa));

			if (Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id)?.Presenter
				is Microsoft.UI.Windowing.OverlappedPresenter apresentador)
			{
				apresentador.Maximize();
			}
		};
#endif

		return janela;
	}
}
