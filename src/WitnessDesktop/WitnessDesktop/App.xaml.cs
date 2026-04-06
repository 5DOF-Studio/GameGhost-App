using WitnessDesktop.Services;

namespace WitnessDesktop;

public partial class App : Application
{
	private const double MainInitialWidth = 1200;
	private const double MainInitialHeight = 900;
	private const double MainMinimumWidth = 900;
	private const double MainMinimumHeight = 720;
	private readonly IStructuralSettingsTracker _structuralSettingsTracker;
	private readonly ISessionTraceService? _sessionTrace;

	public App(IStructuralSettingsTracker structuralSettingsTracker, ISessionTraceService? sessionTrace = null)
	{
		_structuralSettingsTracker = structuralSettingsTracker;
		_sessionTrace = sessionTrace;
		InitializeComponent();

		// Catch unhandled exceptions at the MAUI level — prime suspect for window termination
		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
		{
			CrashLogger.LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception
				?? new Exception($"Non-exception thrown: {e.ExceptionObject}"));
		};

		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			CrashLogger.LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
			// Don't let unobserved task exceptions crash the app
			e.SetObserved();
		};
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		CrashLogger.LogSessionStart();
		CrashLogger.LogLifecycle("App", "CreateWindow called");
		_structuralSettingsTracker.MarkCurrentSettingsApplied();

		var window = new Window(new AppShell())
		{
			Title = "Game Ghost",
			Width = MainInitialWidth,
			Height = MainInitialHeight,
			MinimumWidth = MainMinimumWidth,
			MinimumHeight = MainMinimumHeight,
			MaximumWidth = double.PositiveInfinity,
			MaximumHeight = double.PositiveInfinity
		};

		// Window lifecycle logging — catch what kills the window
		window.Destroying += (_, _) =>
		{
			CrashLogger.LogLifecycle("Window", "Destroying — THIS CAUSES APP TERMINATION on Mac Catalyst");
			_sessionTrace?.EndRun();
		};
		window.Deactivated += (_, _) =>
			CrashLogger.LogLifecycle("Window", "Deactivated");
		window.Stopped += (_, _) =>
			CrashLogger.LogLifecycle("Window", "Stopped");
		window.Resumed += (_, _) =>
			CrashLogger.LogLifecycle("Window", "Resumed");
		window.Backgrounding += (_, _) =>
			CrashLogger.LogLifecycle("Window", "Backgrounding");

		Console.WriteLine($"[App] Window created: {window.Width}x{window.Height}");

		return window;
	}
}
