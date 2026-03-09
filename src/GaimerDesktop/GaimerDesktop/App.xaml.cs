namespace GaimerDesktop;

public partial class App : Application
{
	private const double MainInitialWidth = 1200;
	private const double MainInitialHeight = 900;
	private const double MainMinimumWidth = 900;
	private const double MainMinimumHeight = 720;

	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		Console.WriteLine("[App] CreateWindow called");

		var window = new Window(new AppShell())
		{
			Title = "Gaimer",
			Width = MainInitialWidth,
			Height = MainInitialHeight,
			MinimumWidth = MainMinimumWidth,
			MinimumHeight = MainMinimumHeight,
			MaximumWidth = double.PositiveInfinity,
			MaximumHeight = double.PositiveInfinity
		};

		Console.WriteLine($"[App] Window created: {window.Width}x{window.Height}");

		return window;
	}
}
