using GaimerDesktop.ViewModels;

namespace GaimerDesktop.Views;

public partial class SettingsPage : ContentPage
{
    private string? _originalProvider;
    private string? _originalGender;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SettingsViewModel vm)
        {
            _originalProvider = vm.VoiceProvider;
            _originalGender = vm.VoiceGender;
        }
    }

    private async void OnBackClicked(object? sender, TappedEventArgs e)
    {
        await HandleNavigateBackAsync();
    }

    private async Task HandleNavigateBackAsync()
    {
        if (BindingContext is SettingsViewModel vm)
        {
            bool providerChanged = vm.VoiceProvider != _originalProvider;
            bool genderChanged = vm.VoiceGender != _originalGender;

            if (providerChanged || genderChanged)
            {
                // Check if currently connected via MainViewModel
                var mainVm = Application.Current?.Handler?.MauiContext?.Services.GetService<MainViewModel>();
                if (mainVm?.IsConnected == true)
                {
                    var restart = await DisplayAlert(
                        "Session Restart Required",
                        "Changing the voice configuration will restart your current session.",
                        "Restart", "Cancel");

                    if (!restart)
                    {
                        // Revert to original values
                        vm.VoiceProvider = _originalProvider ?? "gemini";
                        vm.VoiceGender = _originalGender ?? "male";

                        if (Shell.Current is not null)
                            await Shell.Current.GoToAsync("..");
                        return;
                    }

                    // Confirmed: settings already saved (SettingsViewModel writes through),
                    // trigger session restart
                    _ = mainVm.RestartSessionAsync();
                }
            }
        }

        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("..");
    }

    private void OnGeminiSelected(object? sender, TappedEventArgs e)
    {
        if (BindingContext is SettingsViewModel vm)
            vm.VoiceProvider = "gemini";
    }

    private void OnOpenAiSelected(object? sender, TappedEventArgs e)
    {
        if (BindingContext is SettingsViewModel vm)
            vm.VoiceProvider = "openai";
    }

    private void OnMaleSelected(object? sender, TappedEventArgs e)
    {
        if (BindingContext is SettingsViewModel vm)
            vm.VoiceGender = "male";
    }

    private void OnFemaleSelected(object? sender, TappedEventArgs e)
    {
        if (BindingContext is SettingsViewModel vm)
            vm.VoiceGender = "female";
    }
}
