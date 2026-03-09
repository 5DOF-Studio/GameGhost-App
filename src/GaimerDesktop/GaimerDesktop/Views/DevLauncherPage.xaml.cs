namespace GaimerDesktop.Views;

public partial class DevLauncherPage : ContentPage
{
    public DevLauncherPage()
    {
        InitializeComponent();
    }

    private async void OnGaimerAppTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//AgentSelection");
    }

    private async void OnWorkbenchTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("Workbench");
    }
}
