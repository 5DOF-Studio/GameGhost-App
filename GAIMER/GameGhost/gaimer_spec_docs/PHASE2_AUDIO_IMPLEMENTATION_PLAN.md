# Phase 2: Audio System Implementation Plan

**Version:** 1.3.0  
**Date:** December 16, 2025  
**Scope:** Real-time microphone capture, audio playback, and volume monitoring for Windows and macOS platforms  
**Status:** ✅ **CODE COMPLETE** — Awaiting Device Validation  
**Target Completion:** Week 2-3

---

## Revision History

- **1.3.0 (Dec 16, 2025)**: MacCatalyst audio separation implemented (split recording/playback engines) to address slow 24kHz playback (BUG-008). Updated DI to register composite audio service; added new split-service abstractions.
- **1.2.0 (Dec 13, 2025)**: Marked **Code Complete**. Added XML doc comments, Windows microphone capability, bounded playback buffer + overflow logging. Documented deferred recommendations.
- **1.1.1 (Dec 13, 2025)**: Began execution: updated `IAudioService` to spec contract, refactored `MockAudioService` + `MainViewModel`, added initial platform `AudioService` implementations (Windows/NAudio, MacCatalyst/AVAudioEngine), updated DI + macOS permissions.
- **1.1.0 (Dec 13, 2025)**: Added app lifecycle + UI state transition coverage (Minimal ↔ MainView), clarified connection stability responsibilities/contract, tightened macOS permission guidance, expanded test matrix to cover disconnects/backgrounding.
- **1.0.0 (Dec 13, 2025)**: Initial plan.

---

## Completion Summary

| Task | Status | Notes |
|------|--------|-------|
| Task 0: Lifecycle Contract | ✅ Defined | Idempotency rules documented; ViewModel handles state |
| Task 1: Update IAudioService | ✅ Complete | Callback-based, `VolumeChanged`, `ErrorOccurred`, `IDisposable`, sample rates |
| Task 2: Windows AudioService | ✅ Complete | NAudio `WaveInEvent` + `WaveOutEvent`, bounded buffer, overflow logging |
| Task 3: macOS AudioService | ✅ Complete | MacCatalyst now uses split engines (recording + playback) under a composite `IAudioService` to avoid format conflicts and slow playback |
| Task 4: DI Registration | ✅ Complete | `#if WINDOWS / MACCATALYST` conditional in `MauiProgram.cs` (MacCatalyst registers split services + composite) |
| Task 5: Integration Testing | ⏳ Pending | Requires on-device validation (see Testing Checklist) |

### Deferred Recommendations (Post-Testing / Phase 4)

| Recommendation | Reason Deferred |
|----------------|-----------------|
| **Resampling fallback (Windows)** | Low risk—most mics support 16kHz. Handle if a real device fails. |
| **AVAudioEngine grace period (Mac)** | Minor perf optimization; adds complexity. |
| **Output volume decay/smoothing** | UI concern—better handled in ViewModel or converter. |
| **Throttled ErrorOccurred for buffer overflow** | Debug logging is sufficient for now. |

## Overview

This document outlines the implementation plan for Phase 2: Audio System, which enables real-time voice interaction between the user and the Gemini AI. The audio system consists of:

1. **Microphone Capture** - Real-time audio input from user's microphone (16kHz, 16-bit, mono PCM)
2. **Audio Playback** - Playback of AI response audio (24kHz, 16-bit, mono PCM)
3. **Volume Monitoring** - RMS-based volume level calculation for UI visualization

**Reference Specifications:**
- `GAIMER_DESKTOP_APP_SPEC.md` Section 5: Audio System Design
- `gaimer_design_spec.md` Section 8: Audio System

---

## Objectives

- Implement platform-specific audio capture and playback services
- Replace `MockAudioService` with real implementations for Windows and macOS
- Achieve <50ms audio latency for both input and output
- Provide real-time volume level monitoring (RMS calculation)
- Handle device changes, permission requests, and error recovery
- Maintain compatibility with existing `IAudioService` interface

### Non-Goals (Phase 2)

- Implementing Gemini/WebSocket connection management (this is Phase 3), **except** for defining how audio behaves when the connection state changes.
- UI animation polish beyond exposing stable, correctly-scaled volume values (Phase 4).

### Key Assumptions / Invariants

- **Input format**: 16kHz, 16-bit PCM, mono.
- **Output format**: 24kHz, 16-bit PCM, mono (AI response).
- **Threading**: audio capture/playback callbacks fire on background threads; ViewModels must marshal UI updates to the UI thread.
- **No UI-state coupling inside platform services**: platform `AudioService` implementations should be lifecycle-safe and idempotent; UI state transitions (Minimal/MainView) are handled by the ViewModel/app layer calling the service consistently.

---

## Current State

### Existing Interface (`IAudioService.cs`)

```csharp
public interface IAudioService
{
    event EventHandler<float>? InputVolumeChanged;
    event EventHandler<float>? OutputVolumeChanged;
    event EventHandler<byte[]>? AudioCaptured;

    Task StartRecordingAsync();
    Task StopRecordingAsync();
    Task PlayAudioAsync(byte[] audioData);
    Task StopPlaybackAsync();
    bool IsRecording { get; }
    bool IsPlaying { get; }
}
```

### Spec-Aligned Interface (GAIMER_DESKTOP_APP_SPEC.md #5.1)

The Phase 2 implementation must match the interface defined in the specification so downstream consumers (ViewModels, Gemini integration, tests) can rely on a consistent contract. The spec-expanded interface introduces a callback-based recorder, combined volume events, explicit sample rates, and disposable cleanup.

```csharp
public interface IAudioService : IDisposable
{
    // Recording (User microphone)
    Task StartRecordingAsync(Action<byte[]> onAudioCaptured);
    Task StopRecordingAsync();
    bool IsRecording { get; }

    // Playback (AI response)
    Task PlayAudioAsync(byte[] pcmData);
    Task StopPlaybackAsync();
    Task InterruptPlaybackAsync();
    bool IsPlaying { get; }

    // Volume levels
    float InputVolume { get; }
    float OutputVolume { get; }

    // Configuration
    int InputSampleRate { get; }   // 16000 Hz
    int OutputSampleRate { get; }  // 24000 Hz

    // Events
    event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
    event EventHandler<AudioErrorEventArgs>? ErrorOccurred;
}
```

### Current Mock Implementation

- `MockAudioService` simulates volume changes and audio capture
- Generates random volume levels every 100ms
- No real audio processing

### Required Updates to Interface

The spec adds a callback-based recorder, combined volume events, error propagation, explicit sample rates, and disposable cleanup. The interface must match the definition shown above (Section 5.1) before platform services can be implemented to guarantee the rest of the stack (ViewModels, Gemini integration, tests) can consume audio uniformly.

**Decision:** Replace the current interface with the spec-compliant contract before building platform implementations.

---

## Implementation Tasks

### Task 0: Define Audio + App Lifecycle Contract (Minimal ↔ MainView, Backgrounding)

**Goal:** Ensure the plan covers how audio behaves across UI state transitions and app/window lifecycle events so implementation is stable and predictable.

**Why this matters:** The audio layer will appear “buggy” if recording/playback continue unexpectedly when switching to Minimal, minimizing the window, losing focus, or disconnecting. These issues are usually not fixed by platform audio code alone—they require a clear contract between ViewModel/UI state and the audio service.

#### 0.1 State Model (Recommended)

Define these states explicitly in `MainViewModel` (or a dedicated controller):

- **UI State**: `MainView` | `Minimal`
- **Connection State** (Phase 3 integration will drive this): `Disconnected` | `Connecting` | `Connected` | `Reconnecting` | `Failed`
- **Audio State**: `Idle` | `Recording` | `Playing` | `Interrupting` | `Error`

#### 0.2 Transition Rules (Must be implemented in ViewModel/controller)

- **Minimal ↔ MainView**
  - **UI transition must not restart audio by itself.** Switching views should only change visuals; audio continues only if the user/connection state says it should.
  - **Minimal view must remain responsive**: volume meters should continue updating if recording/playing is active; if you decide to pause meters in Minimal, do it explicitly.
- **Window minimized / app deactivated / app suspended**
  - **Default rule**: stop recording and stop playback (privacy + predictable behavior) unless there is an explicit “continue in background” feature (out of Phase 2 scope).
  - On resume: return to `Idle` and require the user to start again (unless product requirements say auto-resume).
- **Navigation / view disposal**
  - Ensure `IAudioService.Dispose()` is called exactly once on app shutdown; do not dispose on view transitions unless the entire app session is ending.

#### 0.3 Idempotency Requirements (AudioService)

Platform `AudioService` must be safe if callers:

- call `StartRecordingAsync()` twice → second call is a no-op or returns a controlled error via `ErrorOccurred`
- call `StopRecordingAsync()` when not recording → no-op
- call `StopPlaybackAsync()` when not playing → no-op
- call `InterruptPlaybackAsync()` anytime → always results in “not playing” and cleared buffers

**Acceptance Criteria:**

- Minimal ↔ MainView transitions do not cause exceptions, duplicated audio sessions, or stuck `IsRecording/IsPlaying` states.
- Minimize/suspend/resume does not leave the microphone “hot” unexpectedly.

#### 0.4 Connection Stability Contract (Audio ↔ Connection Layer)

Even though Phase 3 owns the actual Gemini/WebSocket connection, Phase 2 must define a clear *behavioral contract* so the app remains stable under disconnects/reconnects.

- **Frame sizing (recommended)**
  - Treat **20ms audio frames** as the unit of streaming for input: \(16000 \text{ samples/sec} \times 0.02 \text{ sec} = 320\) samples/frame → **640 bytes** PCM16 mono.
  - If the platform delivers variable chunk sizes, the audio service (or the connection sender) must re-chunk into 20ms frames before sending.
- **Backpressure**
  - The `onAudioCaptured` callback must be **fast/non-blocking** (enqueue + return). Do not do network I/O inside the callback.
  - Use a bounded queue for outgoing audio frames (e.g., cap at ~250–500ms). If exceeded, **drop oldest** (preferred for low-latency conversational UX) and raise a throttled `ErrorOccurred` warning for visibility.
- **Disconnect behavior**
  - On `ConnectionState != Connected`, the app/controller should **stop recording** (default) to avoid silently buffering large amounts of mic audio.
  - If product requirements prefer “record while reconnecting,” make it explicit and bounded (same queue cap + clear user status).
- **Reconnect behavior**
  - Reconnect should not auto-start recording unless the user was actively recording and product requirements explicitly allow auto-resume.

---

### Task 1: Update IAudioService Interface

**Goal:** Align interface with specification requirements

**Steps:**
1. Update `StartRecordingAsync` to accept an `Action<byte[]> onAudioCaptured` callback, remove the obsolete `AudioCaptured` event, and ensure `IAudioService` derives from `IDisposable`.
2. Surface `InputVolume`, `OutputVolume`, `InputSampleRate`, and `OutputSampleRate` as read-only properties and ensure `VolumeChanged` fires with an event args class that carries both measurements.
3. Add `InterruptPlaybackAsync()` and the `ErrorOccurred` event so the ViewModel/UI can react to platform failures immediately.
4. Adjust `MockAudioService` to honor the new callback/event signatures, continue emitting RMS-driven volume values, and surface simulated errors via `AudioErrorEventArgs` when needed for UX testing.
5. Update `MainViewModel` (and any other consumers such as the Phase 3 integration workflow) to pass the audio callback to `StartRecordingAsync` and to consume `VolumeChanged`/`ErrorOccurred` instead of the previous per-channel volume events.

**Files to Modify:**
- `src/GaimerDesktop/GaimerDesktop/Services/IAudioService.cs`
- `src/GaimerDesktop/GaimerDesktop/Services/MockAudioService.cs`
- `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs` (if needed)

**Acceptance Criteria:**
- Interface matches the spec definition verbatim (callback-based recording, `VolumeChanged`/`ErrorOccurred`, sample rates, `IDisposable`, `InterruptPlaybackAsync`)
- Mock service compiles, raises `VolumeChanged`, and can trigger `ErrorOccurred` for UI testing
- ViewModels and integration plans compile and continue to update input/output meters using the new event/callback pattern

---

### Task 2: Windows Audio Implementation (NAudio/WASAPI)

**Goal:** Implement real audio capture and playback on Windows using NAudio library

**Dependencies:**
- NuGet Package: `NAudio` (version 2.2.1 or later)
- Windows 10+ with WASAPI support

**Implementation Steps:**

#### 2.1 Add NAudio Package

```xml
<!-- GaimerDesktop.csproj -->
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0-windows10.0.19041.0'">
  <PackageReference Include="NAudio" Version="2.2.1" />
</ItemGroup>
```

#### 2.2 Create Windows AudioService

**File:** `src/GaimerDesktop/GaimerDesktop/Platforms/Windows/AudioService.cs`

**Key Components:**

1. **Microphone Capture (WASAPI)**
   - Use `WasapiCapture` for microphone input
   - Configure for 16kHz, 16-bit, mono PCM
   - Handle resampling if device doesn't support 16kHz directly
   - Calculate RMS volume on each buffer
   - Invoke the `onAudioCaptured` callback supplied to `StartRecordingAsync` with PCM data slices
   - Update `InputVolume` and fire `VolumeChanged` (carrying both input and output RMS) whenever a new buffer arrives

2. **Audio Playback (WASAPI)**
   - Use `WasapiOut` for audio output
   - Configure for 24kHz, 16-bit, mono PCM
   - Use `BufferedWaveProvider` for queued playback
   - Calculate RMS volume on playback buffers
   - Update `OutputVolume` and raise `VolumeChanged` so the UI can mirror outgoing RMS

   **Note:** Output devices commonly run at 48kHz. If the device/output pipeline cannot accept 24kHz PCM directly, the implementation must resample to the device format internally (while keeping the external contract at 24kHz).

3. **Volume Calculation (RMS)**
   ```csharp
   private float CalculateRMS(byte[] buffer)
   {
       if (buffer.Length < 2) return 0;
       
       float sum = 0;
       for (int i = 0; i < buffer.Length - 1; i += 2)
       {
           short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
           float normalized = sample / 32768f;
           sum += normalized * normalized;
       }
       return (float)Math.Sqrt(sum / (buffer.Length / 2));
   }
   ```

4. **Error Handling**
   - Handle device unplugged events
   - Handle permission denied (microphone access)
   - Re-initialize on device changes
   - Raise the `ErrorOccurred` event with `AudioErrorEventArgs` so the ViewModel can surface friendly messaging

5. **Interruption Support**
   - `InterruptPlaybackAsync()` immediately stops playback and clears buffer
   - Used when user speaks during AI response

**Reference Implementation:** See `GAIMER_DESKTOP_APP_SPEC.md` Section 5.2

**Files to Create:**
- `src/GaimerDesktop/GaimerDesktop/Platforms/Windows/AudioService.cs`

**Files to Modify:**
- `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs` (register Windows AudioService)

**Acceptance Criteria:**
- Microphone capture works with default input device
- Audio playback works with default output device
- Volume levels update in real-time (UI shows IN/OUT percentages)
- RMS calculation accurate (0-1 range)
- Handles device changes gracefully
- Interruption stops playback immediately

---

### Task 3: macOS Audio Implementation (AVAudioEngine)

**Goal:** Implement real audio capture and playback on macOS using AVAudioEngine

**Dependencies:**
- macOS 12.0+ with AVAudioEngine support
- Microphone permission (only)

**Implementation Steps:**

#### 3.1 Configure Permissions

**File:** `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/Info.plist`

```xml
<key>NSMicrophoneUsageDescription</key>
<string>Gaimer needs microphone access for voice chat with the AI.</string>
```

**File:** `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/Entitlements.plist`

```xml
<key>com.apple.security.device.audio-input</key>
<true/>
```

#### 3.2 Create macOS AudioService

**File:** `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/AudioService.cs`

**Key Components:**

1. **Microphone Capture (AVAudioEngine)**
   - Create `AVAudioEngine` instance
   - Get `AVAudioInputNode` from engine
   - Install tap on input bus with 16kHz mono format
   - Extract PCM data from `AVAudioPcmBuffer`
   - Calculate RMS volume
   - Invoke the `onAudioCaptured` callback supplied to `StartRecordingAsync` with PCM data slices
   - Update `InputVolume` and fire `VolumeChanged` (carrying both input and output RMS)

2. **Audio Playback (AVAudioEngine)**
   - Create `AVAudioPlayerNode` attached to engine
   - Configure output format: 24kHz, 16-bit, mono
   - Schedule buffers for playback
   - Calculate RMS volume on scheduled buffers
   - Update `OutputVolume` and raise `VolumeChanged`

3. **PCM Data Extraction**
   ```csharp
   private byte[] ExtractPcmData(AVAudioPcmBuffer buffer)
   {
       var audioBufferList = buffer.AudioBufferList;
       if (audioBufferList.Count == 0) return Array.Empty<byte>();
       
       var audioBuffer = audioBufferList[0];
       var dataLength = (int)audioBuffer.DataByteSize;
       
       if (dataLength == 0) return Array.Empty<byte>();
       
       var data = new byte[dataLength];
       Marshal.Copy(audioBuffer.Data, data, 0, dataLength);
       return data;
   }
   ```

4. **Volume Calculation (RMS)**
   - Same RMS algorithm as Windows implementation
   - Handle 16-bit PCM samples

5. **Error Handling**
   - Handle permission denied (request permission + surface friendly error)
   - Handle engine start errors
   - Handle device changes

**Permission Guidance (Mac Catalyst):**

- For Mac Catalyst, declare `NSMicrophoneUsageDescription` and request access before starting the engine.
- Prefer requesting permission via `AVCaptureDevice.RequestAccessForMediaType(AVMediaType.Audio, ...)` (or the most appropriate API available in your target framework) and raise `ErrorOccurred` if denied.

**Reference Implementation:** See `GAIMER_DESKTOP_APP_SPEC.md` Section 5.3

**Files to Create:**
- `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/AudioService.cs`

**Files to Modify:**
- `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/Info.plist`
- `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/Entitlements.plist`
- `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs` (register macOS AudioService)

**Acceptance Criteria:**
- Microphone capture works after permission grant
- Audio playback works with default output device
- Volume levels update in real-time
- Handles permission requests gracefully
- Interruption stops playback immediately

---

### Task 4: Update Dependency Injection

**Goal:** Register platform-specific AudioService implementations

**File:** `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs`

**Changes:**
```csharp
private static void RegisterServices(IServiceCollection services)
{
#if WINDOWS
    services.AddSingleton<IAudioService, Platforms.Windows.AudioService>();
#elif MACCATALYST
    services.AddSingleton<IAudioService, Platforms.MacCatalyst.AudioService>();
#else
    // Fallback to mock for other platforms
    services.AddSingleton<IAudioService, MockAudioService>();
#endif
    
    services.AddSingleton<IWindowCaptureService, MockWindowCaptureService>();
    services.AddSingleton<IGeminiService, MockGeminiService>();
}
```

**Acceptance Criteria:**
- Correct service registered based on platform
- Build succeeds on both Windows and macOS
- Mock service still available for testing

---

### Task 5: Integration Testing

**Goal:** Verify audio system works end-to-end

**Test Scenarios:**

1. **Microphone Capture**
   - Start recording → verify the `StartRecordingAsync` callback receives PCM buffers and that `VolumeChanged` reports the same RMS levels
   - Speak into microphone → verify volume levels change and update both UI meters
   - Stop recording → verify the callback stops firing and `IsRecording` returns false

2. **Audio Playback**
   - Play test PCM data → verify audio plays
   - Verify volume levels update during playback
   - Test interruption → verify immediate stop

3. **Volume Monitoring**
   - Verify IN% and OUT% display in UI
   - Verify volume bars animate (if implemented)
   - Test with different audio levels

4. **Error Handling**
   - Test with microphone unplugged (Windows)
   - Test without microphone permission (macOS)
   - Test device changes during recording

5. **Performance**
   - Measure latency (target <50ms)
   - Monitor CPU usage
   - Monitor memory usage

6. **UI State Transitions (Minimal ↔ MainView)**
   - Switch from MainView → Minimal while recording: recording continues (or stops) exactly per the contract, without duplicating sessions; meters behave as designed
   - Switch from Minimal → MainView while playing: playback continues and UI reflects state correctly
   - Rapid toggling (10+ times): no leaks, no duplicated events, no stuck `IsRecording/IsPlaying`

7. **Lifecycle (Minimize / Background / Resume)**
   - Minimize/lose focus while recording: microphone turns off per contract; UI state updates; no “hot mic”
   - Resume: audio returns to `Idle` (or auto-resumes if explicitly required) without requiring app restart

8. **Connection Stability (Disconnect/Reconnect) — Contract Validation**
   - Simulate connection drop while recording: ViewModel stops recording and surfaces user-facing status; no crashes
   - Reconnect: recording does not auto-start unless explicitly triggered; audio service remains usable

**Files to Create:**
- Test plan document (optional)
- Manual test checklist

**Acceptance Criteria:**
- All test scenarios pass
- Latency meets target (<50ms)
- No memory leaks
- Graceful error handling

---

## Audio Configuration Summary

| Parameter | Input | Output |
|-----------|-------|--------|
| Sample Rate | 16,000 Hz | 24,000 Hz |
| Bit Depth | 16-bit | 16-bit |
| Channels | 1 (Mono) | 1 (Mono) |
| Format | PCM | PCM |
| Frame/Chunk Size (recommended) | 20ms frames (320 samples / 640 bytes PCM16) | 20–40ms frames (implementation-defined) |
| Latency Target | <50ms | <50ms |

---

## Platform-Specific Notes

### Windows (WASAPI)

**Advantages:**
- Low-latency audio capture/playback
- Automatic device management
- Good error reporting

**Challenges:**
- May need resampling if device doesn't support 16kHz
- Device change events require re-initialization
- Permission handling via Windows settings

**Key APIs:**
- `WasapiCapture` - Microphone capture
- `WasapiOut` - Audio playback
- `MediaFoundationResampler` - Resampling if needed

### macOS (AVAudioEngine)

**Advantages:**
- Unified audio engine for capture and playback
- Good permission handling
- Native macOS integration

**Challenges:**
- Permission requests required
- PCM buffer extraction requires marshalling
- Engine lifecycle management

**Key APIs:**
- `AVAudioEngine` - Audio engine
- `AVAudioInputNode` - Microphone input
- `AVAudioPlayerNode` - Audio playback
- `AVAudioPcmBuffer` - PCM data container

---

## Error Handling Strategy

### Common Errors

1. **Permission Denied**
   - **Windows:** Check microphone privacy settings
   - **macOS (Mac Catalyst):** Request permission via appropriate platform API (e.g., `AVCaptureDevice`), surface denial clearly
   - **Recovery:** Show user-friendly message, guide to settings

2. **Device Unplugged**
   - **Windows:** Handle `RecordingStopped` event
   - **macOS:** Handle engine errors
   - **Recovery:** Notify user, stop recording gracefully

3. **Device Changed**
   - **Windows:** Re-enumerate devices, reinitialize capture
   - **macOS:** Reinitialize engine
   - **Recovery:** Auto-reconnect to new default device

4. **Format Not Supported**
   - **Windows:** Use resampler to convert to 16kHz
   - **macOS:** Configure format conversion
   - **Recovery:** Automatic resampling

---

## Testing Checklist

- [ ] Windows microphone capture works
- [ ] macOS microphone capture works (with permission)
- [ ] Windows audio playback works
- [ ] macOS audio playback works
- [ ] Volume levels update in real-time (IN%)
- [ ] Volume levels update during playback (OUT%)
- [ ] RMS calculation accurate (0-1 range)
- [ ] Interruption stops playback immediately
- [ ] Device changes handled gracefully
- [ ] Permission requests work (macOS)
- [ ] Error messages user-friendly
- [ ] No memory leaks during long sessions
- [ ] Latency <50ms for both input and output
- [ ] Minimal ↔ MainView transitions do not break recording/playback or leak resources
- [ ] Minimize/background/resume behaves per contract (no unexpected recording/playback)
- [ ] Disconnect/reconnect does not crash and leaves audio usable

---

## Next Steps After Phase 2

Once audio system is complete:

1. **Phase 3: Integration** - Connect audio to Gemini API
2. **Phase 4: Polish** - Optional: upgrade audio visualizer rendering (SkiaSharp) beyond the baseline bound visualizer

---

## Documentation Updates

After completing Phase 2, update:

- `GAIMER/GameGhost/PROGRESS_LOG.md` - Mark Phase 2 tasks complete
- `GAIMER/GameGhost/FEATURE_LIST.md` - Update audio feature status
- Code comments in `AudioService` implementations

---

**Notes:**
- Audio system is critical path for voice interaction
- Focus on low latency and reliability
- Test thoroughly on both platforms before moving to Phase 3
- Keep mock service available for development/testing

