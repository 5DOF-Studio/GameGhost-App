# Audio Separation Implementation Plan

**Created:** December 15, 2025  
**Status:** ✅ Implemented & Validated  
**Priority:** High  
**Estimated Effort:** 2-3 hours  
**Related Bugs:** BUG-007 (✅ Fixed), BUG-008 (✅ Fixed), BUG-009 (✅ Fixed)  
**Commits:** `1a075d8` (implementation), `17dd641` (docs)

---

## Problem Statement

The current `AudioService` on MacCatalyst uses a **single AVAudioEngine** for both recording and playback. This causes:

1. **Format conflicts:** Recording uses 48kHz native, playback receives 24kHz from OpenAI
2. **Slow playback:** Converting 24kHz → 44.1kHz (mixer format) uses fractional ratio (1.8375x)
3. **Echo leakage:** Slow playback extends window for mic to capture AI voice
4. **Complex code:** Single class managing two independent audio paths

---

## Solution: Separate Recording and Playback Services

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      IAudioService                          │
│  (Unified interface - keeps existing consumers unchanged)   │
└─────────────────────────┬───────────────────────────────────┘
                          │ delegates to
          ┌───────────────┴───────────────┐
          │                               │
          ▼                               ▼
┌─────────────────────┐         ┌─────────────────────┐
│ IAudioRecordingService │         │ IAudioPlaybackService │
│  - StartRecordingAsync │         │  - PlayAudioAsync     │
│  - StopRecordingAsync  │         │  - StopPlaybackAsync  │
│  - IsRecording         │         │  - IsPlaying          │
│  - InputVolume         │         │  - OutputVolume       │
└─────────────────────┘         └─────────────────────┘
          │                               │
          ▼                               ▼
┌─────────────────────┐         ┌─────────────────────┐
│ MacCatalyst:        │         │ MacCatalyst:        │
│ RecordingService    │         │ PlaybackService     │
│                     │         │                     │
│ AVAudioEngine #1    │         │ AVAudioEngine #2    │
│ - InputNode → Tap   │         │ - PlayerNode        │
│ - 48kHz native      │         │ - 24kHz native      │
│ - Convert to 16kHz  │         │ - No conversion!    │
└─────────────────────┘         └─────────────────────┘
```

### Key Benefits

| Benefit | Description |
|---------|-------------|
| **No format conflicts** | Each engine configured for single purpose |
| **No sample rate conversion** | Playback engine runs at 24kHz natively |
| **Correct playback speed** | Audio plays at intended rate |
| **Reduced echo** | Faster playback = smaller capture window |
| **Cleaner code** | Each class has single responsibility |
| **Independent lifecycle** | Start/stop recording without affecting playback |
| **Easier debugging** | Isolate issues to specific component |

---

## Implementation Steps

### Step 1: Create New Interfaces

**File:** `src/GaimerDesktop/GaimerDesktop/Services/Audio/IAudioRecordingService.cs`

```csharp
namespace GaimerDesktop.Services.Audio;

public interface IAudioRecordingService : IDisposable
{
    bool IsRecording { get; }
    float InputVolume { get; }
    
    event EventHandler<float>? InputVolumeChanged;
    event EventHandler<AudioErrorEventArgs>? ErrorOccurred;
    
    Task StartRecordingAsync(Action<byte[]> onAudioCaptured);
    Task StopRecordingAsync();
}
```

**File:** `src/GaimerDesktop/GaimerDesktop/Services/Audio/IAudioPlaybackService.cs`

```csharp
namespace GaimerDesktop.Services.Audio;

public interface IAudioPlaybackService : IDisposable
{
    bool IsPlaying { get; }
    float OutputVolume { get; }
    
    event EventHandler<float>? OutputVolumeChanged;
    event EventHandler<AudioErrorEventArgs>? ErrorOccurred;
    
    Task PlayAudioAsync(byte[] pcmData);
    Task StopPlaybackAsync();
}
```

### Step 2: Create MacCatalyst Recording Service

**File:** `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/RecordingService.cs`

Extract recording logic from current `AudioService.cs`:
- AVAudioSession setup (PlayAndRecord category)
- AVAudioEngine for input only
- Input tap with 48kHz → 16kHz conversion
- Volume calculation (RMS)

### Step 3: Create MacCatalyst Playback Service

**File:** `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/PlaybackService.cs`

Create new playback-only service:
- **Separate AVAudioEngine** instance
- Configure for **24kHz output natively**
- AVAudioPlayerNode for scheduling buffers
- Volume calculation (RMS)
- No format conversion needed

**Key Configuration:**
```csharp
// Configure engine for 24kHz playback
var playbackFormat = new AVAudioFormat(
    AVAudioCommonFormat.PCMInt16, 
    24000, // 24kHz - matches OpenAI output
    1,     // mono
    interleaved: true
);

_playerNode = new AVAudioPlayerNode();
_engine.AttachNode(_playerNode);
_engine.Connect(_playerNode, _engine.MainMixerNode, playbackFormat);
```

### Step 4: Create Composite AudioService

**File:** `src/GaimerDesktop/GaimerDesktop/Services/Audio/CompositeAudioService.cs`

Wraps both services to maintain `IAudioService` compatibility:

```csharp
public sealed class CompositeAudioService : IAudioService
{
    private readonly IAudioRecordingService _recording;
    private readonly IAudioPlaybackService _playback;

    public bool IsRecording => _recording.IsRecording;
    public bool IsPlaying => _playback.IsPlaying;
    public float InputVolume => _recording.InputVolume;
    public float OutputVolume => _playback.OutputVolume;

    public Task StartRecordingAsync(Action<byte[]> onAudioCaptured) 
        => _recording.StartRecordingAsync(onAudioCaptured);

    public Task StopRecordingAsync() 
        => _recording.StopRecordingAsync();

    public Task PlayAudioAsync(byte[] pcmData) 
        => _playback.PlayAudioAsync(pcmData);

    public Task StopPlaybackAsync() 
        => _playback.StopPlaybackAsync();

    // ... event forwarding ...
}
```

### Step 5: Update DI Registration

**File:** `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs`

```csharp
#if MACCATALYST
    services.AddSingleton<IAudioRecordingService, RecordingService>();
    services.AddSingleton<IAudioPlaybackService, PlaybackService>();
    services.AddSingleton<IAudioService, CompositeAudioService>();
#endif
```

### Step 6: Update MainViewModel (Optional Enhancement)

**File:** `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs`

With separate services, echo suppression can be more granular:

```csharp
// Option A: Check playback state (current approach, still works)
if (_conversationProvider.IsConnected && !_audioService.IsPlaying)
{
    _ = _conversationProvider.SendAudioAsync(pcm);
}

// Option B: Use separate service references (more explicit)
if (_conversationProvider.IsConnected && !_playbackService.IsPlaying)
{
    _ = _conversationProvider.SendAudioAsync(pcm);
}
```

---

## Files to Create

| File | Description |
|------|-------------|
| `Services/Audio/IAudioRecordingService.cs` | Recording interface |
| `Services/Audio/IAudioPlaybackService.cs` | Playback interface |
| `Platforms/MacCatalyst/RecordingService.cs` | MacCatalyst recording implementation |
| `Platforms/MacCatalyst/PlaybackService.cs` | MacCatalyst playback implementation |
| `Services/Audio/CompositeAudioService.cs` | Wrapper maintaining IAudioService compatibility |
| `Platforms/MacCatalyst/MacAudioSessionManager.cs` | Shared ref-counted AVAudioSession lease manager |

## Files to Modify

| File | Changes |
|------|---------|
| `MauiProgram.cs` | Update DI registration for new services |
| `Platforms/MacCatalyst/AudioService.cs` | Can be deprecated after migration |

## Files Unchanged

| File | Reason |
|------|--------|
| `IAudioService.cs` | Interface preserved for compatibility |
| `MainViewModel.cs` | Uses IAudioService unchanged |
| `Platforms/Windows/AudioService.cs` | Windows implementation separate |

---

## Testing Checklist

### Recording Tests
- [x] Microphone captures audio at 16kHz
- [x] InputVolume updates correctly
- [x] StartRecordingAsync/StopRecordingAsync work
- [x] Permission handling works

### Playback Tests
- [x] Audio plays at correct speed (via manual resampling)
- [x] OutputVolume updates correctly
- [x] PlayAudioAsync queues buffers correctly
- [x] StopPlaybackAsync interrupts cleanly

### Integration Tests
- [x] Recording and playback work simultaneously
- [x] Echo suppression (IsPlaying check) works
- [x] MainViewModel integration unchanged
- [x] OpenAI provider works end-to-end
- [ ] Gemini provider still works (not tested this session)

### Regression Tests
- [x] Connect/disconnect flows work
- [x] MinimalView shows correct audio levels
- [ ] No memory leaks on repeated connect/disconnect (not tested)

---

## Rollback Plan

If issues arise:
1. Revert to commit before this change (commit hash in PROGRESS_LOG)
2. The old `AudioService.cs` with workaround (format converter) still works
3. Current state is functional (just slower playback)

---

## Success Criteria

1. ✅ Audio playback at correct speed (no slowdown)
2. ✅ Echo suppression works effectively
3. ✅ No format conversion errors
4. ✅ All existing functionality preserved
5. ✅ Cleaner, more maintainable code

---

## Notes

- Windows implementation (`Platforms/Windows/AudioService.cs`) can be refactored similarly if needed
- Future: Could add echo cancellation (AEC) for even better results
- Future: Could use separate audio devices (mic vs speakers) for complete isolation

---

## Implementation Notes (Actual)

### What Changed

- `IAudioService` remains the app-facing contract; MacCatalyst now registers a composite that delegates to separate recording + playback services.
- Recording uses its own AVAudioEngine input tap (native 48kHz rate) and converts to 16kHz PCM16 mono.
- Playback uses its own AVAudioEngine + AVAudioPlayerNode connected with the **mixer's native format** (44.1kHz Float32 stereo).
- **Key insight:** AVAudioEngine's MainMixerNode cannot be connected with arbitrary formats—it uses the hardware output format. The solution uses manual linear interpolation resampling (24kHz PCM16 → 44.1kHz Float32) before scheduling buffers.
- AVAudioSession activation/deactivation is ref-counted via `MacAudioSessionManager` so recording and playback can overlap safely.
- A pending-buffer counter keeps `IsPlaying` stable while queued audio remains.

### Key Code: Manual Resampling (PlaybackService.ConvertToMixerFormat)

```csharp
// Resample 24kHz PCM16 mono → mixer's native format (44.1kHz Float32 stereo)
for (int outIdx = 0; outIdx < outputSamples; outIdx++)
{
    // Linear interpolation resampling
    double srcPos = (double)outIdx * inputSampleRate / outputSampleRate;
    int srcIdx = (int)srcPos;
    double frac = srcPos - srcIdx;
    
    // Read and interpolate PCM16 samples
    short s0 = (short)(pcm16Data[srcIdx * 2] | (pcm16Data[srcIdx * 2 + 1] << 8));
    short s1 = (short)(pcm16Data[nextIdx * 2] | (pcm16Data[nextIdx * 2 + 1] << 8));
    float sample = (float)((s0 + (s1 - s0) * frac) / 32768.0);
    
    // Write to stereo channels
    Marshal.Copy(resampledFloats, 0, leftChannelPtr, outputSamples);
    Marshal.Copy(resampledFloats, 0, rightChannelPtr, outputSamples);
}
```

### Files (Actual)

- **Added**
  - `src/GaimerDesktop/GaimerDesktop/Services/Audio/IAudioRecordingService.cs`
  - `src/GaimerDesktop/GaimerDesktop/Services/Audio/IAudioPlaybackService.cs`
  - `src/GaimerDesktop/GaimerDesktop/Services/Audio/CompositeAudioService.cs`
  - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/MacAudioSessionManager.cs`
  - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/RecordingService.cs`
  - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/PlaybackService.cs`
- **Modified**
  - `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs` (MacCatalyst DI now uses the split services)
- **Deprecated (not used by DI but kept for reference)**
  - `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/AudioService.cs`

---

## Future Enhancements

| Enhancement | Description | Priority |
|-------------|-------------|----------|
| **Hardware Echo Cancellation** | Use `AVAudioUnitEQ` or system AEC for better echo suppression | Medium |
| **Voice Activity Detection (Client-Side)** | Local VAD to reduce network traffic when silent | Low |
| **Audio Device Selection** | Allow user to select specific microphone/speaker | Low |
| **Windows Audio Separation** | Apply same pattern to Windows `AudioService.cs` | Medium |
| **Audio Level Normalization** | Automatic gain control for consistent volume | Low |
| **Buffer Size Tuning** | Configurable buffer sizes for latency optimization | Low |

### Enhancement Details

#### Hardware Echo Cancellation
The current echo suppression relies on the `IsPlaying` check to mute the microphone while AI is speaking. This is effective but could be improved with hardware-level AEC:
- macOS: `AVAudioSession.Mode.VoiceChat` enables system echo cancellation
- Requires testing to ensure it doesn't interfere with game audio capture

#### Windows Audio Separation
The Windows platform currently uses a monolithic `AudioService`. The same separation pattern could be applied:
- Create `Windows/RecordingService.cs` using NAudio's `WaveInEvent`
- Create `Windows/PlaybackService.cs` using NAudio's `WaveOutEvent`
- Register split services in `MauiProgram.cs` for Windows TFM

---

## Validation Results (December 15, 2025)

| Test | Result |
|------|--------|
| Audio plays at correct speed | ✅ Verified |
| No echo/feedback loop | ✅ Verified |
| Multiple conversations stable | ✅ Verified |
| Clean disconnect (no crash) | ✅ Verified |
| State preserved across views | ✅ Verified |

**Tester Comment:** "very smooth, very nice, very stable"

