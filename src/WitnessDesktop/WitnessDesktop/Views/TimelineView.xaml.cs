using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using WitnessDesktop.Models;
using WitnessDesktop.Models.Timeline;
using WitnessDesktop.Services;
using WitnessDesktop.ViewModels;

namespace WitnessDesktop.Views;

public partial class TimelineView : ContentView
{
    private sealed class VideoPlaybackState
    {
        public TimelineEvent? Event { get; set; }
        public PropertyChangedEventHandler? EventHandler { get; set; }
    }

    private readonly ConditionalWeakTable<MediaElement, VideoPlaybackState> _videoPlaybackStates = new();
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
            _timelineFeed.EventAdded += OnEventAdded;
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        UnsubscribeFromTimelineFeed();
        Unloaded -= OnUnloaded;
    }

    private void OnVideoBindingContextChanged(object? sender, EventArgs e)
    {
        if (sender is not MediaElement mediaElement)
            return;

        SyncVideoPlaybackState(mediaElement, stopPreviousPlayback: true);
    }

    private void OnVideoMediaLoaded(object? sender, EventArgs e)
    {
        if (sender is not MediaElement mediaElement)
            return;

        SyncVideoPlaybackState(mediaElement, stopPreviousPlayback: false);
    }

    private async void OnVideoMediaOpened(object? sender, EventArgs e)
    {
        if (sender is not MediaElement mediaElement)
            return;

        await StartVideoPlaybackAsync(mediaElement);
    }

    private void OnVideoMediaEnded(object? sender, EventArgs e)
    {
        if (sender is MediaElement mediaElement)
        {
            StopVideo(mediaElement);
        }
    }

    private void OnVideoPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        if (sender is not MediaElement mediaElement)
            return;

        if (!TryGetVideoPlaybackWindow(mediaElement, out var start, out var end))
            return;

        if (e.Position >= end && end > start)
        {
            StopVideo(mediaElement);
        }
    }

    private void OnVideoMediaUnloaded(object? sender, EventArgs e)
    {
        if (sender is MediaElement mediaElement)
        {
            ClearVideoPlaybackState(mediaElement);
            StopVideo(mediaElement);
        }
    }

    private void SyncVideoPlaybackState(MediaElement mediaElement, bool stopPreviousPlayback)
    {
        var playbackState = _videoPlaybackStates.GetOrCreateValue(mediaElement);
        var previousEvent = playbackState.Event;
        var currentEvent = mediaElement.BindingContext as TimelineEvent;

        if (ReferenceEquals(previousEvent, currentEvent) && playbackState.EventHandler is not null)
            return;

        if (previousEvent is not null && playbackState.EventHandler is not null)
        {
            previousEvent.PropertyChanged -= playbackState.EventHandler;
        }

        if (stopPreviousPlayback && previousEvent is not null)
        {
            StopVideo(mediaElement);
        }

        playbackState.Event = currentEvent;
        playbackState.EventHandler = null;

        if (currentEvent?.Media?.Type != MediaContentType.Video)
            return;

        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName != nameof(TimelineEvent.IsExpanded))
                return;

            if (currentEvent.IsExpanded)
            {
                _ = StartVideoPlaybackAsync(mediaElement);
            }
            else
            {
                StopVideo(mediaElement);
            }
        };

        playbackState.EventHandler = handler;
        currentEvent.PropertyChanged += handler;
    }

    private void ClearVideoPlaybackState(MediaElement mediaElement)
    {
        if (!_videoPlaybackStates.TryGetValue(mediaElement, out var playbackState))
            return;

        if (playbackState.Event is not null && playbackState.EventHandler is not null)
        {
            playbackState.Event.PropertyChanged -= playbackState.EventHandler;
        }

        playbackState.Event = null;
        playbackState.EventHandler = null;
    }

    private async Task StartVideoPlaybackAsync(MediaElement mediaElement)
    {
        if (!TryGetVideoPlaybackWindow(mediaElement, out var start, out _))
            return;

        try
        {
            if (start > TimeSpan.Zero)
            {
                await mediaElement.SeekTo(start, CancellationToken.None);
            }
        }
        catch
        {
            // A seek failure should not break the card; fall back to best-effort playback.
        }

        try
        {
            mediaElement.Play();
        }
        catch
        {
            // Keep the timeline responsive even if the platform player rejects play().
        }
    }

    private static bool TryGetVideoPlaybackWindow(
        MediaElement mediaElement,
        out TimeSpan start,
        out TimeSpan end)
    {
        start = default;
        end = default;

        if (mediaElement.BindingContext is not TimelineEvent evt || evt.Media?.Type != MediaContentType.Video)
            return false;

        var media = evt.Media!;
        var startSeconds = Math.Max(0, media.StartTime);
        var durationSeconds = media.Duration;

        start = TimeSpan.FromSeconds(startSeconds);
        end = durationSeconds > 0
            ? TimeSpan.FromSeconds(startSeconds + durationSeconds)
            : TimeSpan.MaxValue;

        return true;
    }

    private static void StopVideo(MediaElement mediaElement)
    {
        if (mediaElement.CurrentState is MediaElementState.None or MediaElementState.Stopped)
            return;

        try
        {
            mediaElement.Stop();
        }
        catch
        {
            // Recycling should never fail because a platform player refused to stop.
        }
    }

    private void UnsubscribeFromTimelineFeed()
    {
        if (_timelineFeed != null)
        {
            _timelineFeed.EventAdded -= OnEventAdded;
            _timelineFeed = null;
        }
    }

    private void OnEventAdded(object? sender, TimelineEvent evt)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TimelineCollection.ScrollTo(evt, position: ScrollToPosition.End, animate: true);
        });
    }
}
