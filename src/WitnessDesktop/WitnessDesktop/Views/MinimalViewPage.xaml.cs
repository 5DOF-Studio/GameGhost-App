using System.Timers;
using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Views;

public partial class MinimalViewPage : ContentPage
{
    private MainViewModel? _viewModel;
    private System.Timers.Timer? _dismissTimer;

    public MinimalViewPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        
        // Use MainViewModel (singleton) for shared state
        _viewModel = Application.Current?.Handler?.MauiContext?.Services.GetService<MainViewModel>();
        if (_viewModel != null)
        {
            BindingContext = _viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // Start timer if content already exists
            if (_viewModel.HasPanelContent)
            {
                StartDismissTimer();
            }
        }
        
        // Window resize is handled by MainViewModel.NavigateToMinimalViewAsync()
    }
    
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        
        // Unsubscribe from property changes
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
        
        // Stop dismiss timer
        StopDismissTimer();
    }
    
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SlidingPanelContent) || 
            e.PropertyName == nameof(MainViewModel.HasPanelContent))
        {
            if (_viewModel != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (_viewModel.HasPanelContent)
                    {
                        StartDismissTimer();
                    }
                    else
                    {
                        StopDismissTimer();
                    }
                });
            }
        }
    }
    
    private void StartDismissTimer()
    {
        StopDismissTimer();
        
        if (_viewModel?.SlidingPanelContent != null)
        {
            var dismissMs = _viewModel.SlidingPanelContent.AutoDismissMs;
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
    }
    
    private void StopDismissTimer()
    {
        _dismissTimer?.Stop();
        _dismissTimer?.Dispose();
        _dismissTimer = null;
    }
}
