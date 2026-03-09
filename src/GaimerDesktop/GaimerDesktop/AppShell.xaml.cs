using GaimerDesktop.Views;

namespace GaimerDesktop;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("MainPage", typeof(MainPage));
        Routing.RegisterRoute("MinimalView", typeof(MinimalViewPage));
        Routing.RegisterRoute("Settings", typeof(SettingsPage));
        Routing.RegisterRoute("Unauthorized", typeof(UnauthorizedPage));
        Routing.RegisterRoute("AgentSelection", typeof(AgentSelectionPage));
        Routing.RegisterRoute("Error", typeof(ErrorPage));

        DevTab.IsVisible = false;
    }
}
