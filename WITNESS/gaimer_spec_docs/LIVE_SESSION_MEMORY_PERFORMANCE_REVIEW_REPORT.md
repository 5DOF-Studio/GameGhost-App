# Live Session Memory and Performance Review

**Project:** Gaimer Desktop / Witness Desktop  
**Date:** March 11, 2026  
**Scope:** Long-running live session behavior only  
**Status:** Proposal document for secondary review  
**Constraint:** No fixes implemented from this report

---

## Summary

This review focused on hot-path behavior for long-running single-session gameplay:

- voice capture and playback
- realtime websocket flows
- screen capture and frame processing
- UI event routing and session state retention

The main conclusion is that the app is structurally capable of running a single live session, but several code paths are still optimized for correctness over bounded runtime cost. That is acceptable for early development, but it conflicts with the intended operating model:

- one active session
- hours of uptime
- only about five minutes of live in-memory state
- older state summarized, virtualized, or discarded

The biggest risks are:

1. unbounded transient collections
2. repeated full-frame image transforms before change detection
3. long-lived UI/background activity with weak cleanup ownership
4. sustained allocation churn in audio and image hot paths

---

## Intended Runtime Model

Based on product guidance for this review:

- only one window, one game, one connection at a time
- the app may stay connected for hours
- live chat/images/transient events should not remain in memory beyond roughly five minutes
- virtualization/summarization is the desired boundary
- some agents may later require much higher frame rates than chess

This means the correct design target is not "archive the session in memory." The correct design target is:

```csharp
// Proposal-level policy, not current code.
const int LiveRetentionMinutes = 5;

// Live state should remain bounded.
// Older state should be summarized or virtualized.
```

---

## Findings

### 1. Unbounded live session state conflicts with the 5-minute retention goal

**Severity:** High  
**Impact:** memory growth over hours, especially in chat-heavy sessions

#### Relevant files

- [MainViewModel.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs)
- [TimelineFeed.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/TimelineFeed.cs)
- [VisualReelService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/VisualReelService.cs)

#### Current code examples

`ChatMessages` is append-only for the session:

```csharp
public ObservableCollection<ChatMessage> ChatMessages { get; } = new();
```

Messages are inserted repeatedly with no trimming:

```csharp
ChatMessages.Insert(0, message);
```

Timeline checkpoints and events also grow without retention logic:

```csharp
Checkpoints.Insert(0, checkpoint);
existingLine.Events.Add(evt);
```

#### Why this matters

This directly conflicts with the intended runtime model. For hours-long sessions, even modest append rates become a memory policy bug, not an optimization issue.

#### Proposal

Introduce hard retention windows for all live session structures.

Illustrative direction:

```csharp
private static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(5);
private const int MaxLiveChatMessages = 200;

private void TrimChatMessages()
{
    while (ChatMessages.Count > MaxLiveChatMessages)
    {
        ChatMessages.RemoveAt(ChatMessages.Count - 1);
    }
}
```

And for timeline:

```csharp
private void TrimTimeline(DateTime utcNow)
{
    // Keep only checkpoints/events inside live working set.
    // Older material should be summarized or virtualized.
}
```

#### Recommendation

Make retention policy explicit and centralized. Do not rely on disconnect to be the only cleanup point.

---

### 2. The capture pipeline does expensive work before it knows whether the frame matters

**Severity:** High  
**Impact:** CPU cost, GC churn, poor scaling for higher-frequency agents

#### Relevant files

- [MainViewModel.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs)
- [ImageProcessor.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/ImageProcessor.cs)
- [FrameDiffService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/FrameDiffService.cs)
- [Platforms/MacCatalyst/WindowCaptureService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/WindowCaptureService.cs)
- [Platforms/Windows/WindowCaptureService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Platforms/Windows/WindowCaptureService.cs)

#### Current hot-path shape

Today the frame path effectively does:

```csharp
_visualReelService.Append(moment);

var previewFrame = ImageProcessor.ScaleToHeight(rawFrame, 360);
PreviewImage = previewFrame;

var compressed = ImageProcessor.ScaleAndCompress(rawFrame);
if (!_frameDiffService.HasChanged(compressed, diffThreshold))
{
    return;
}

_brainService.TrySubmitFrame(compressed, contextStr);
```

This means the system:

1. captures a frame
2. creates preview JPEG
3. creates compressed JPEG
4. decodes again for dHash
5. only then decides whether the frame is useful

#### Why this matters

For chess this is survivable. For agents that may need 1-2 frames every 2-3 seconds, and especially for future video-like behavior, this becomes one of the primary bottlenecks.

#### Proposal

Split capture into two layers:

1. cheap change-detection path
2. expensive downstream processing path

Illustrative direction:

```csharp
var rawFrame = CaptureNativeFrame();

if (!changeGate.ShouldProcess(rawFrame, utcNow))
{
    return;
}

var preview = BuildPreview(rawFrame);
var compressed = BuildBrainFrame(rawFrame);
SubmitToBrain(compressed);
```

Possible refinement:

```csharp
// For chess, hash ROI or board-local crop first.
var boardHash = fastHasher.Compute(rawFrame, boardRegion);
```

#### Recommendation

Move change detection as early as possible, ideally before any full preview/compression work.

---

### 3. UI-owned background loops and timers need stronger lifecycle ownership

**Severity:** High  
**Impact:** latent leaks, duplicate UI work, thread churn over long uptime

#### Relevant files

- [Controls/AudioLevelMeter.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Controls/AudioLevelMeter.cs)
- [Views/MinimalViewPage.xaml.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Views/MinimalViewPage.xaml.cs)
- [Views/FabOverlayView.xaml.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Views/FabOverlayView.xaml.cs)
- [Views/GaimerHudView.xaml.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Views/GaimerHudView.xaml.cs)
- [MainViewModel.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs)

#### Current code examples

`AudioLevelMeter` starts a free-running worker:

```csharp
_ = Task.Run(async () =>
{
    while (!token.IsCancellationRequested)
    {
        MainThread.BeginInvokeOnMainThread(() => SetFirstLedPulse(opacity));
        await Task.Delay(60, token).ConfigureAwait(false);
    }
}, token);
```

Several views create their own timers:

```csharp
_dismissTimer = new System.Timers.Timer(dismissMs);
_dismissTimer.Elapsed += async (_, _) =>
{
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
        _viewModel?.DismissSlidingPanelCommand.Execute(null);
    });
};
```

`MainViewModel` subscribes to many singleton services, but does not currently expose explicit teardown:

```csharp
_captureService.FrameCaptured += ...
_conversationProvider.TextReceived += ...
_audioService.VolumeChanged += ...
```

#### Why this matters

Even in a single-session app, long-lived UI objects should not rely on implicit GC cleanup. If a control or VM outlives its intended visual lifetime, it can keep generating work indefinitely.

#### Proposal

Move UI pulse behavior to view lifecycle or native animation primitives, and require explicit attach/detach semantics for service subscriptions.

Illustrative direction:

```csharp
public sealed class AudioLevelMeter : ContentView, IDisposable
{
    public void Dispose()
    {
        _pulseCts?.Cancel();
        _pulseCts?.Dispose();
    }
}
```

And for `MainViewModel`:

```csharp
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    public void Dispose()
    {
        _captureService.FrameCaptured -= OnFrameCaptured;
        _conversationProvider.TextReceived -= OnTextReceived;
        _audioService.VolumeChanged -= OnVolumeChanged;
    }
}
```

#### Recommendation

Treat background activity as owned resources, not incidental side effects of property changes.

---

### 4. Audio playback and capture have sustained allocation pressure

**Severity:** Medium  
**Impact:** long-session GC pressure, jitter during heavy voice output

#### Relevant files

- [Platforms/MacCatalyst/PlaybackService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/PlaybackService.cs)
- [Platforms/MacCatalyst/RecordingService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/RecordingService.cs)
- [Services/Audio/AudioResampler.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/Audio/AudioResampler.cs)

#### Current code examples

Playback allocates a fresh float array per buffer:

```csharp
var resampledFloats = new float[outputSamples];
Marshal.Copy(resampledFloats, 0, leftChannelPtr, outputSamples);
```

Recording converts float buffers sample-by-sample:

```csharp
for (int i = 0; i < frameLength; i++)
{
    float mono = 0f;
    ...
    output[i * 2] = (byte)(s & 0xFF);
}
```

#### Why this matters

This is not obviously leaking memory, but it is a sustained churn source in the hottest continuous audio paths.

#### Proposal

Reduce allocation frequency through reusable buffers or pooled transform helpers.

Illustrative direction:

```csharp
// Proposal only
var rented = ArrayPool<float>.Shared.Rent(outputSamples);
try
{
    // fill rented span
}
finally
{
    ArrayPool<float>.Shared.Return(rented);
}
```

#### Recommendation

This is a second-wave optimization after retention and capture gating are fixed, but it should be planned before higher-rate agents.

---

### 5. Capture services contain blocking and allocation-heavy platform code that will limit future rate increases

**Severity:** Medium  
**Impact:** blocked worker threads, expensive native/managed conversions

#### Relevant files

- [Platforms/MacCatalyst/WindowCaptureService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/WindowCaptureService.cs)
- [Platforms/Windows/WindowCaptureService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Platforms/Windows/WindowCaptureService.cs)

#### Current code examples

MacCatalyst waits synchronously for capture callback:

```csharp
if (!tcs.Task.Wait(TimeSpan.FromSeconds(10)))
{
    return Array.Empty<byte>();
}
```

Windows capture allocates full pixel arrays per frame:

```csharp
var pixelData = new byte[width * height * 4];
```

#### Why this matters

The current approach is acceptable for relatively low-rate capture, but it is not shaped for frequent capture or future streaming scenarios.

#### Proposal

Make capture scheduling and capture processing explicitly separate:

```csharp
// Proposal only
captureLoop -> native frame acquisition -> lightweight gate -> bounded processing queue
```

And avoid synchronous waits on hot capture threads where possible.

#### Recommendation

Do not treat current capture behavior as a foundation for higher-rate agents without reworking the flow.

---

### 6. Realtime services are operationally noisy on hot paths

**Severity:** Low  
**Impact:** logging overhead, noisy diagnostics, harder profiling

#### Relevant files

- [OpenAIRealtimeService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/OpenAIRealtimeService.cs)
- [GeminiLiveService.cs](/Users/tonynlemadim/Documents/5DOF%20Projects/gAImer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/GeminiLiveService.cs)

#### Current code examples

```csharp
Console.WriteLine($"[OpenAI] Sent audio chunk #{count}: {audioData.Length} bytes");
Console.WriteLine($"[Gemini] Received message #{count}: {json.Length} chars");
```

#### Why this matters

This is not the root of any memory issue, but it will distort hot-path behavior under live traffic and makes it harder to isolate real performance costs.

#### Proposal

Use structured sampling or debug-only logging for chunk/frame traffic:

```csharp
if (count % 500 == 0)
{
    logger.LogDebug("Audio chunks sent: {Count}", count);
}
```

---

## Proposed Solution Set

## Proposal A: Bounded Live Working Set

### Goal

Match runtime behavior to the intended 5-minute session model.

### Recommended targets

- `ChatMessages`
- timeline checkpoints/events
- preview image history
- transient event/error lists

### Suggested policy

```csharp
public sealed record LiveRetentionPolicy(
    TimeSpan Window,
    int MaxChatMessages,
    int MaxTimelineCheckpoints,
    int MaxEventsPerLine
);
```

### Review note

This is the most important architectural change because it aligns the code with product intent.

---

## Proposal B: Early Change Detection Before Full Image Processing

### Goal

Avoid expensive transforms for frames that will be dropped anyway.

### Suggested shape

```csharp
var rawFrame = CaptureFrame();

if (!captureGate.ShouldEmit(rawFrame, utcNow))
{
    return;
}

var preview = BuildPreview(rawFrame);
var compressed = CompressForBrain(rawFrame);
SubmitToBrain(compressed);
```

### Review note

This is the key scalability step for non-chess agents and future video-like use cases.

---

## Proposal C: Lifecycle-Owned Cleanup

### Goal

Prevent long-lived background work from outliving the visual/session object that created it.

### Suggested shape

```csharp
public interface IAttachableViewModel
{
    void Attach();
    void Detach();
}
```

Or:

```csharp
public sealed class SomeView : ContentView, IDisposable
{
    public void Dispose()
    {
        timer?.Stop();
        timer?.Dispose();
        subscription?.Dispose();
    }
}
```

### Review note

This is less about the current single-window design and more about making the runtime safe under long uptime and lifecycle edge cases.

---

## Proposal D: Hot-Path Allocation Reduction

### Goal

Reduce GC churn in audio and image transforms.

### Candidate areas

- playback sample conversion
- recording sample conversion
- frame compression
- preview generation
- websocket send buffers if reuse is practical

### Suggested tools

- `ArrayPool<T>`
- reusable scratch buffers
- separation of diff path and render/compress path

---

## Proposal E: Operational Metrics for Long Sessions

### Goal

Make regressions measurable before full persistence infrastructure exists.

### Suggested counters

```csharp
capture_live_frames
capture_dropped_frames
brain_queue_wait_ms
chat_live_count
timeline_live_count
gc_pause_count
realtime_reconnect_count
```

### Review note

Since infrastructure is still evolving, lightweight in-app telemetry is the fastest way to catch long-session degradation.

---

## Suggested Review Order

For a secondary reviewer, the recommended order is:

1. Verify that bounded retention is required by product intent.
2. Review whether early diff gating can be moved ahead of JPEG/preview generation.
3. Review lifetime ownership of UI timers/background loops.
4. Review audio allocation hot paths.
5. Review platform capture code for blocking behavior under future higher-rate capture.

---

## Open Review Questions

These are questions for design review, not implementation:

1. Should the 5-minute retention rule be enforced by count, time, or both?
2. Should chat virtualization happen continuously, or only on thresholds?
3. For chess, should change detection move toward board-only ROI hashing rather than full-frame hashing?
4. Should UI preview cadence be capped separately from capture cadence?
5. Should the future video-like path bypass current screenshot-oriented transforms entirely?

---

## Conclusion

The current code is serviceable for a single live session, but it is not yet aligned with the intended operating model of:

- long uptime
- short live-memory window
- increasing future capture rates

The most important review conclusion is simple:

**The system should be redesigned around a bounded live working set, not around session-long accumulation.**

Everything else in this report follows from that principle.
