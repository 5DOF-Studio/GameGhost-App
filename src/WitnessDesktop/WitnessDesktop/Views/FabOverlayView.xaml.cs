using System.Timers;
using WitnessDesktop.Models;
using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Views;

public partial class FabOverlayView : ContentView
{
    private MainViewModel? _viewModel;
    private System.Timers.Timer? _dismissTimer;

    public FabOverlayView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(MainViewModel vm)
    {
        DetachViewModel();

        _viewModel = vm;
        BindingContext = vm;
        vm.PropertyChanged += ViewModel_PropertyChanged;
    }

    public void DetachViewModel()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel = null;
        }

        StopDismissTimer();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.FabCardVariant) ||
            e.PropertyName == nameof(MainViewModel.IsFabCardVisible))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_viewModel?.IsFabCardVisible == true && _viewModel.ShowTextCard)
                {
                    StartDismissTimer();
                }
                else if (_viewModel?.FabCardVariant == FabCardVariant.None)
                {
                    StopDismissTimer();
                }
            });
        }
    }

    private void StartDismissTimer()
    {
        StopDismissTimer();

        var dismissMs = _viewModel?.SlidingPanelContent?.AutoDismissMs ?? 5000;
        _dismissTimer = new System.Timers.Timer(dismissMs);
        _dismissTimer.Elapsed += async (_, _) =>
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _viewModel?.DismissSlidingPanelCommand.Execute(null);
            });
        };
        _dismissTimer.AutoReset = false;
        _dismissTimer.Start();
    }

    private void StopDismissTimer()
    {
        _dismissTimer?.Stop();
        _dismissTimer?.Dispose();
        _dismissTimer = null;
    }
}
