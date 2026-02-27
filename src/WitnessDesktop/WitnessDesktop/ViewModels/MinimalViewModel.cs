using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.ViewModels;

public partial class MinimalViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private System.Timers.Timer? _dismissTimer;

    public MinimalViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        
        // Subscribe to sliding panel content changes to handle auto-dismiss
        _mainViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SlidingPanelContent))
            {
                OnPropertyChanged(nameof(SlidingPanelContent));
                OnPropertyChanged(nameof(HasPanelContent));
                
                if (_mainViewModel.SlidingPanelContent != null)
                {
                    StartAutoDismissTimer();
                }
                else
                {
                    StopAutoDismissTimer();
                }
            }
        };
    }

    // Delegate properties to MainViewModel
    public Agent? SelectedAgent => _mainViewModel.SelectedAgent;
    public CaptureTarget? CurrentTarget => _mainViewModel.CurrentTarget;
    public float InputVolume => _mainViewModel.InputVolume;
    public float OutputVolume => _mainViewModel.OutputVolume;
    public SlidingPanelContent? SlidingPanelContent => _mainViewModel.SlidingPanelContent;
    public bool IsConnected => _mainViewModel.IsConnected;
    public bool HasPanelContent => _mainViewModel.HasPanelContent;

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _mainViewModel.ToggleConnectionCommand.ExecuteAsync(null);
        // Window resize will be handled by MainViewModel when navigating back
    }

    [RelayCommand]
    private async Task ExpandToMainViewAsync()
    {
        await _mainViewModel.ExpandToMainViewCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void DismissSlidingPanel()
    {
        _mainViewModel.DismissSlidingPanelCommand.Execute(null);
    }
    
    private void StartAutoDismissTimer()
    {
        StopAutoDismissTimer();
        
        if (_mainViewModel.SlidingPanelContent != null)
        {
            var dismissMs = _mainViewModel.SlidingPanelContent.AutoDismissMs;
            _dismissTimer = new System.Timers.Timer(dismissMs);
            _dismissTimer.Elapsed += async (_, _) =>
            {
                await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _mainViewModel.DismissSlidingPanelCommand.Execute(null);
                });
            };
            _dismissTimer.AutoReset = false;
            _dismissTimer.Start();
        }
    }
    
    private void StopAutoDismissTimer()
    {
        _dismissTimer?.Stop();
        _dismissTimer?.Dispose();
        _dismissTimer = null;
    }
}

