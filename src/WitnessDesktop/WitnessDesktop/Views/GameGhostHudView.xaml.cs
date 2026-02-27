using System.Timers;
using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Views;

public partial class GameGhostHudView : ContentView
{
    private MainViewModel? _viewModel;
    private System.Timers.Timer? _dismissTimer;

    public static readonly BindableProperty IsExpandedProperty =
        BindableProperty.Create(nameof(IsExpanded), typeof(bool), typeof(GameGhostHudView), false,
            propertyChanged: OnIsExpandedChanged);

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public string ChevronText => IsExpanded ? "\u25B2" : "\u25BC";

    public GameGhostHudView()
    {
        InitializeComponent();
    }

    public void AttachViewModel(MainViewModel vm)
    {
        DetachViewModel();

        _viewModel = vm;
        BindingContext = vm;
        vm.PropertyChanged += ViewModel_PropertyChanged;

        // If content already exists, auto-expand and start timer
        if (vm.HasPanelContent)
        {
            IsExpanded = true;
            StartDismissTimer();
        }
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
        if (e.PropertyName == nameof(MainViewModel.SlidingPanelContent) ||
            e.PropertyName == nameof(MainViewModel.HasPanelContent))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_viewModel?.HasPanelContent == true)
                {
                    IsExpanded = true;
                    StartDismissTimer();
                }
                else
                {
                    StopDismissTimer();
                }
            });
        }
    }

    private void OnToggleExpand(object? sender, EventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private static void OnIsExpandedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GameGhostHudView hud)
        {
            var expanded = (bool)newValue;
            hud.ContentArea.IsVisible = expanded;
            hud.TagsRow.IsVisible = expanded && (hud._viewModel?.HasPanelContent ?? false);
            hud.OnPropertyChanged(nameof(ChevronText));
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
