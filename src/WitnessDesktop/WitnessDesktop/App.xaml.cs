namespace WitnessDesktop;

public partial class App : Application
{
    // NOTE: Window sizing in MAUI uses device-independent units (points), not literal pixels.
    // For investigation we start larger than the spec so we can easily observe if the size is being applied.
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
        // Main Dashboard (resizable): start at a large size, but enforce a minimum.
        // This helps debug whether the app is honoring the configured size vs platform scaling/state restoration.
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

        return window;
    }
}
