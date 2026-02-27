using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Views;

public partial class WorkbenchPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public WorkbenchPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        BindingContext = _viewModel;
        HudView.AttachViewModel(_viewModel);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        HudView.DetachViewModel();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnHudViewPillTapped(object? sender, TappedEventArgs e)
    {
        // Already showing HUD View — no-op for now.
        // Future: swap visible component in the preview area.
    }
}
