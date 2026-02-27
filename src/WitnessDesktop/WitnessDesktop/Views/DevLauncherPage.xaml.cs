namespace WitnessDesktop.Views;

public partial class DevLauncherPage : ContentPage
{
    public DevLauncherPage()
    {
        InitializeComponent();
    }

    private async void OnGameGhostAppTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//AgentSelection");
    }

    private async void OnWorkbenchTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("Workbench");
    }
}
