using GaimerDesktop.Services;

namespace GaimerDesktop.Views;

public partial class UnauthorizedPage : ContentPage
{
    public UnauthorizedPage()
    {
        InitializeComponent();
        LoadDeviceId();
    }

    private async void LoadDeviceId()
    {
        try
        {
            var settings = Application.Current?.Handler?.MauiContext?.Services.GetService<ISettingsService>();
            if (settings != null)
            {
                var deviceId = await settings.GetDeviceIdAsync();
                DeviceIdLabel.Text = deviceId;
            }
        }
        catch
        {
            DeviceIdLabel.Text = "unavailable";
        }
    }
}
