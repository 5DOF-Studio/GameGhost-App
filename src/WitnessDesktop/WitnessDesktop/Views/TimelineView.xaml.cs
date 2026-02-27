using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;
using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Views;

public partial class TimelineView : ContentView
{
    private ITimelineFeed? _timelineFeed;
    
    public TimelineView()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }
    
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        
        UnsubscribeFromTimelineFeed();
        
        if (BindingContext is MainViewModel vm && vm.TimelineFeed != null)
        {
            _timelineFeed = vm.TimelineFeed;
            _timelineFeed.CheckpointCreated += OnCheckpointCreated;
        }
    }
    
    private void OnUnloaded(object? sender, EventArgs e)
    {
        UnsubscribeFromTimelineFeed();
        Unloaded -= OnUnloaded;
    }
    
    private void UnsubscribeFromTimelineFeed()
    {
        if (_timelineFeed != null)
        {
            _timelineFeed.CheckpointCreated -= OnCheckpointCreated;
            _timelineFeed = null;
        }
    }
    
    private void OnCheckpointCreated(object? sender, TimelineCheckpoint checkpoint)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TimelineCollection.ScrollTo(checkpoint, position: ScrollToPosition.End, animate: true);
        });
    }
}
