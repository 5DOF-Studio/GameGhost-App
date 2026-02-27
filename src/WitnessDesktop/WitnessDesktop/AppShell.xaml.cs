using WitnessDesktop.Views;

namespace WitnessDesktop;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("MainPage", typeof(MainPage));
        Routing.RegisterRoute("MinimalView", typeof(MinimalViewPage));

        DevTab.IsVisible = false;
    }
}
