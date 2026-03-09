# Audio Device Selection Research Report
**Feature:** Independent Audio Input/Output Device Selection  
**Date:** December 23, 2025  
**Platforms:** Windows Desktop & macOS (MacCatalyst)

---

## Executive Summary

This research validates the **feasibility and implementation approach** for allowing users to independently select:
- **Audio Input Device** (microphone source): Built-in mic, Bluetooth headset mic, USB mic, etc.
- **Audio Output Device** (speaker/headphone destination): System speakers, Bluetooth speakers, USB headphones, etc.

**Key Finding:** ✅ **Fully Supported** on both Windows and macOS desktop platforms with platform-specific APIs.

---

## Feature Requirements

### 1. Independent Device Selection
- User can select input and output devices separately
- Devices are not coupled (e.g., speak into Bluetooth mic, hear from system speakers)

### 2. Device Enumeration
- Application must list all available audio input devices
- Application must list all available audio output devices
- Include device names and identifiers

### 3. Dynamic Device Switching
- Allow changing devices during an active conversation (optional for v1.0)
- Handle device connection/disconnection gracefully

### 4. Platform Support
- **Primary:** Windows Desktop, macOS (MacCatalyst)
- **Future:** iOS, Android (not in current scope)

---

## Platform-Specific Implementation

### Windows Implementation

#### Current State (AudioService.cs)
Your current Windows implementation uses:
- **NAudio library** with `WaveInEvent` (microphone) and `WaveOutEvent` (playback)
- **Default device only** - no device selection capability yet

#### Required Changes for Device Selection

**1. Device Enumeration:**

```csharp
using NAudio.Wave;
using System.Collections.Generic;

public class AudioDeviceInfo
{
    public int DeviceNumber { get; set; }
    public string DeviceName { get; set; }
    public string DeviceId { get; set; }
}

// Enumerate Input Devices
public static List<AudioDeviceInfo> GetInputDevices()
{
    var devices = new List<AudioDeviceInfo>();
    for (int i = 0; i < WaveInEvent.DeviceCount; i++)
    {
        var capabilities = WaveInEvent.GetCapabilities(i);
        devices.Add(new AudioDeviceInfo
        {
            DeviceNumber = i,
            DeviceName = capabilities.ProductName,
            DeviceId = capabilities.ProductGuid.ToString()
        });
    }
    return devices;
}

// Enumerate Output Devices
public static List<AudioDeviceInfo> GetOutputDevices()
{
    var devices = new List<AudioDeviceInfo>();
    for (int i = 0; i < WaveOutEvent.DeviceCount; i++)
    {
        var capabilities = WaveOutEvent.GetCapabilities(i);
        devices.Add(new AudioDeviceInfo
        {
            DeviceNumber = i,
            DeviceName = capabilities.ProductName,
            DeviceId = capabilities.ProductGuid.ToString()
        });
    }
    return devices;
}
```

**2. Device Selection:**

```csharp
// Modified StartRecordingAsync to accept device number
public Task StartRecordingAsync(Action<byte[]> onAudioCaptured, int inputDeviceNumber = 0)
{
    // ... existing validation ...
    
    _waveIn = new WaveInEvent
    {
        DeviceNumber = inputDeviceNumber,  // ← SELECT SPECIFIC DEVICE
        WaveFormat = new WaveFormat(InputSampleRate, 16, 1),
        BufferMilliseconds = 20
    };
    
    // ... rest of implementation ...
}

// Modified playback initialization to accept device number
private void EnsurePlaybackInitialized_NoLock(int outputDeviceNumber = 0)
{
    if (_waveOut != null && _playbackBuffer != null) return;
    
    var format = new WaveFormat(OutputSampleRate, 16, 1);
    _playbackBuffer = new BufferedWaveProvider(format)
    {
        BufferLength = 24000,
        DiscardOnBufferOverflow = true
    };
    
    _waveOut = new WaveOutEvent
    {
        DeviceNumber = outputDeviceNumber,  // ← SELECT SPECIFIC DEVICE
        DesiredLatency = 100
    };
    
    _waveOut.Init(_playbackBuffer);
    // ... rest of implementation ...
}
```

**Key Points:**
- NAudio makes this **trivial** - just set `DeviceNumber` property
- Device enumeration is built into NAudio
- No need for raw WASAPI unless you need ultra-low latency or advanced features

---

### macOS (MacCatalyst) Implementation

#### Current State (AudioService.cs - MacCatalyst)
Your current macOS implementation uses:
- **AVAudioEngine** with `AVAudioInputNode` (microphone) and `AVAudioPlayerNode` (playback)
- **Uses AVAudioSession** for iOS audio stack compatibility
- **Default device only** - no device selection capability yet

#### Required Changes for Device Selection

**Challenge:** AVAudioEngine on iOS/MacCatalyst uses the **system default** input/output devices. Unlike macOS native apps, you cannot directly select audio devices via `AVAudioEngine` API.

**Solution Approaches:**

##### Option 1: AVAudioSession Routing (Recommended for MacCatalyst)
```objc
// This is the iOS/MacCatalyst way - configure routing preferences
var audioSession = AVAudioSession.SharedInstance();

// Configure category with options for Bluetooth
var categoryOptions = AVAudioSessionCategoryOptions.DefaultToSpeaker |
                      AVAudioSessionCategoryOptions.AllowBluetooth |
                      AVAudioSessionCategoryOptions.AllowBluetoothA2DP;

audioSession.SetCategory(
    AVAudioSessionCategory.PlayAndRecord,
    AVAudioSessionMode.Default,
    categoryOptions,
    out NSError categoryError);

// Override output to specific port (Speaker, Bluetooth, etc.)
audioSession.OverrideOutputAudioPort(
    AVAudioSessionPortOverride.Speaker,
    out NSError overrideError);
```

**Limitations:**
- Limited to system-provided routing options
- Cannot enumerate and select arbitrary devices like on Windows
- User must configure preferred Bluetooth device in System Settings

##### Option 2: Core Audio HAL (Native macOS Only - NOT MacCatalyst)
```objc
// This ONLY works in native macOS apps (not MacCatalyst)
// Requires kAudioHardwarePropertyDevices API

AudioObjectPropertyAddress propertyAddress = {
    kAudioHardwarePropertyDevices,
    kAudioObjectPropertyScopeGlobal,
    kAudioObjectPropertyElementMaster
};

UInt32 dataSize = 0;
AudioObjectGetPropertyDataSize(kAudioObjectSystemObject, &propertyAddress, 0, NULL, &dataSize);

UInt32 deviceCount = dataSize / sizeof(AudioDeviceID);
AudioDeviceID *audioDevices = (AudioDeviceID *)malloc(dataSize);
AudioObjectGetPropertyData(kAudioObjectSystemObject, &propertyAddress, 0, NULL, &dataSize, audioDevices);

// Set default device
AudioObjectPropertyAddress defaultInputAddress = {
    kAudioHardwarePropertyDefaultInputDevice,
    kAudioObjectPropertyScopeGlobal,
    kAudioObjectPropertyElementMaster
};

AudioObjectSetPropertyData(
    kAudioObjectSystemObject,
    &defaultInputAddress,
    0, NULL,
    sizeof(AudioDeviceID),
    &selectedDeviceID);
```

**Problem:** This API is **NOT available in MacCatalyst** because MacCatalyst uses the iOS audio stack, not the macOS audio stack.

##### Option 3: Hybrid Approach (Recommended)
```csharp
// In IAudioService, add device selection methods:
public interface IAudioService
{
    // Existing methods...
    
    // NEW: Device enumeration and selection
    Task<List<AudioDeviceInfo>> GetAvailableInputDevicesAsync();
    Task<List<AudioDeviceInfo>> GetAvailableOutputDevicesAsync();
    Task SetInputDeviceAsync(string deviceId);
    Task SetOutputDeviceAsync(string deviceId);
}
```

**Windows Implementation:** Full device enumeration and selection via NAudio

**MacCatalyst Implementation:**
- Enumerate available audio routes via `AVAudioSession.CurrentRoute`
- Switch between Speaker, Bluetooth, Receiver via `AVAudioSession.OverrideOutputAudioPort`
- For input, respect system's default (user sets in System Settings)
- Provide UI guidance: "To use a different microphone, set it as default in System Settings"

---

## Technical Considerations

### 1. WebSocket Real-Time Audio Compatibility

**Good News:** Device selection happens **before** audio data enters your WebSocket pipeline:

```
[Selected Mic Device] → [AudioService Capture] → [PCM Buffer] → [WebSocket Send]
[WebSocket Receive] → [PCM Buffer] → [AudioService Playback] → [Selected Speaker Device]
```

- Gemini Live API and OpenAI Realtime API receive/send **raw PCM audio data**
- They don't care which physical device you used to capture/play
- Device selection is purely a client-side concern

### 2. Format Conversion

Your current implementation already handles format conversion:
- **Input:** Hardware native format → Convert to 16kHz/16-bit/mono PCM
- **Output:** 24kHz/16-bit/mono PCM → Convert to hardware native format

Different devices may have different native formats (48kHz, 44.1kHz, etc.), but your conversion pipeline handles this.

### 3. Latency Considerations

- **Windows:** NAudio's `WaveInEvent`/`WaveOutEvent` have ~20-100ms latency (acceptable)
- **macOS:** AVAudioEngine with 20ms buffer is already optimized for low latency
- Device selection doesn't significantly impact latency if format conversion is efficient

### 4. Bluetooth Device Challenges

**Common Issues:**
1. **Connection stability** - Bluetooth can disconnect mid-conversation
2. **Latency** - Bluetooth adds 100-200ms latency (A2DP), less for HFP
3. **Echo cancellation** - May need acoustic echo cancellation (AEC) for speakerphone scenarios

**Recommendations:**
- Add error handling for device disconnection
- Display latency warnings for Bluetooth devices
- Consider AEC if echo becomes an issue

---

## Recommended Implementation Plan

### Phase 1: Windows Full Support (Easiest)
1. ✅ Add device enumeration methods to `IAudioService`
2. ✅ Modify Windows `AudioService.cs` to accept `deviceNumber` parameters
3. ✅ Create `AudioDeviceInfo` model class
4. ✅ Add device selection UI (dropdown menus)
5. ✅ Persist user's device preferences (LocalSettings or config file)

**Effort:** ~4-8 hours (straightforward with NAudio)

### Phase 2: MacCatalyst Partial Support
1. ✅ Implement `AVAudioSession.CurrentRoute` inspection for available routes
2. ✅ Add output routing override (Speaker vs Bluetooth)
3. ✅ Provide UI guidance for input device selection via System Settings
4. ✅ Handle route change notifications (when user connects/disconnects Bluetooth)

**Effort:** ~8-12 hours (requires careful AVAudioSession management)

### Phase 3: Enhanced Features (Optional)
1. ⬜ Dynamic device switching during active conversation
2. ⬜ Device hot-plug/unplug detection and auto-reconnection
3. ⬜ Per-device volume controls
4. ⬜ Audio device testing/monitoring UI (mic test, speaker test)

**Effort:** ~12-20 hours

---

## API Design Proposal

### Updated IAudioService Interface

```csharp
public interface IAudioService : IDisposable
{
    // ===== EXISTING METHODS =====
    Task StartRecordingAsync(Action<byte[]> onAudioCaptured);
    Task StopRecordingAsync();
    bool IsRecording { get; }
    
    Task PlayAudioAsync(byte[] pcmData);
    Task StopPlaybackAsync();
    Task InterruptPlaybackAsync();
    bool IsPlaying { get; }
    
    float InputVolume { get; }
    float OutputVolume { get; }
    int InputSampleRate { get; }
    int OutputSampleRate { get; }
    
    event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
    event EventHandler<AudioErrorEventArgs>? ErrorOccurred;
    
    // ===== NEW: DEVICE SELECTION =====
    
    /// <summary>
    /// Gets a list of available audio input devices (microphones).
    /// </summary>
    Task<IReadOnlyList<AudioDeviceInfo>> GetAvailableInputDevicesAsync();
    
    /// <summary>
    /// Gets a list of available audio output devices (speakers/headphones).
    /// </summary>
    Task<IReadOnlyList<AudioDeviceInfo>> GetAvailableOutputDevicesAsync();
    
    /// <summary>
    /// Gets the currently selected input device (null if using default).
    /// </summary>
    AudioDeviceInfo? CurrentInputDevice { get; }
    
    /// <summary>
    /// Gets the currently selected output device (null if using default).
    /// </summary>
    AudioDeviceInfo? CurrentOutputDevice { get; }
    
    /// <summary>
    /// Selects the audio input device to use for recording.
    /// Must be called before StartRecordingAsync() or will take effect on next recording session.
    /// </summary>
    /// <param name="deviceId">Device ID from GetAvailableInputDevicesAsync(), or null for system default</param>
    Task SetInputDeviceAsync(string? deviceId);
    
    /// <summary>
    /// Selects the audio output device to use for playback.
    /// Can be called during active playback (device will switch after current buffer finishes).
    /// </summary>
    /// <param name="deviceId">Device ID from GetAvailableOutputDevicesAsync(), or null for system default</param>
    Task SetOutputDeviceAsync(string? deviceId);
    
    /// <summary>
    /// Raised when available audio devices change (device connected/disconnected).
    /// </summary>
    event EventHandler? AudioDevicesChanged;
}
```

### AudioDeviceInfo Model

```csharp
public class AudioDeviceInfo
{
    /// <summary>
    /// Unique device identifier (platform-specific).
    /// </summary>
    public required string DeviceId { get; init; }
    
    /// <summary>
    /// User-friendly device name (e.g., "Microphone (Realtek High Definition Audio)").
    /// </summary>
    public required string DeviceName { get; init; }
    
    /// <summary>
    /// Device type (Built-in, USB, Bluetooth, etc.).
    /// </summary>
    public AudioDeviceType DeviceType { get; init; }
    
    /// <summary>
    /// True if this is the system's default device.
    /// </summary>
    public bool IsDefault { get; init; }
    
    /// <summary>
    /// True if the device is currently connected and available.
    /// </summary>
    public bool IsAvailable { get; init; }
}

public enum AudioDeviceType
{
    Unknown,
    BuiltIn,
    USB,
    Bluetooth,
    Virtual,
    Network
}
```

---

## UI Design Considerations

### Settings Panel or Dropdown
```
┌─ Audio Settings ────────────────────────┐
│                                          │
│  🎤 Microphone                           │
│  ┌──────────────────────────────────┐   │
│  │ ▼ Built-in Microphone (Default) │   │
│  │   Bluetooth Headset - AirPods    │   │
│  │   USB Microphone - Blue Yeti     │   │
│  └──────────────────────────────────┘   │
│                                          │
│  🔊 Speakers                             │
│  ┌──────────────────────────────────┐   │
│  │ ▼ System Speakers (Default)     │   │
│  │   Bluetooth Speaker - JBL Flip   │   │
│  │   Headphones - Sony WH-1000XM4   │   │
│  └──────────────────────────────────┘   │
│                                          │
│  [Test Microphone] [Test Speakers]      │
│                                          │
└──────────────────────────────────────────┘
```

### In-Game HUD (Minimal)
```
┌─ Quick Audio ───────┐
│ 🎤 [AirPods    ▼]  │
│ 🔊 [Speakers   ▼]  │
└─────────────────────┘
```

---

## Cross-Platform Library Consideration

### PortAudio (C/C++ Library)
**Pros:**
- Unified API for Windows, macOS, Linux
- Low-level device enumeration and selection
- Very low latency

**Cons:**
- C/C++ library - requires P/Invoke or C++/CLI wrapper
- Adds complexity to build process
- Your current NAudio (Windows) and AVAudioEngine (Mac) stack works well

**Recommendation:** ❌ **Not recommended** - your current approach is cleaner for .NET MAUI

---

## Security & Privacy Considerations

### Permissions
- **Windows:** No special permissions (user already granted mic access)
- **macOS:** Microphone permission already requested via `NSMicrophoneUsageDescription`
- **Bluetooth:** No additional permissions on either platform

### Privacy
- Device enumeration reveals connected devices (Bluetooth names may be sensitive)
- Consider privacy: don't log full device names in telemetry

---

## Testing Strategy

### Test Cases
1. **Default devices** - Works without any selection (existing behavior)
2. **Single device switch** - Change mic, keep speaker default
3. **Both devices changed** - Custom mic + custom speakers
4. **Device disconnection** - Unplug USB mic mid-conversation
5. **Bluetooth pairing** - Connect Bluetooth headset during active session
6. **Multiple USB devices** - 2+ USB audio interfaces connected
7. **No devices available** - Edge case (unlikely but possible)

### Platform-Specific Tests
- **Windows:** Test with USB headset, Bluetooth speaker, virtual audio cable
- **macOS:** Test with AirPods, USB mic, built-in speakers

---

## Open Questions & Decisions Needed

### 1. Device Selection Timing
- **Option A:** User selects before starting conversation (simpler)
- **Option B:** User can switch mid-conversation (more complex)

**Recommendation:** Start with Option A, add Option B in Phase 3

### 2. Handling Device Disconnection
- **Option A:** Auto-fallback to default device
- **Option B:** Pause conversation and prompt user

**Recommendation:** Option A with notification to user

### 3. Persisting Device Preferences
- **Option A:** Remember last used devices (better UX)
- **Option B:** Always default to system default (simpler)

**Recommendation:** Option A - save to `Preferences` API or local JSON

---

## Code Structure Recommendations

### New Files to Create
```
Services/
├── Audio/
│   ├── IAudioService.cs (UPDATE - add device methods)
│   ├── AudioDeviceInfo.cs (NEW)
│   └── AudioDeviceType.cs (NEW)
│
├── Platforms/
│   ├── Windows/
│   │   └── AudioService.cs (UPDATE - add device selection)
│   └── MacCatalyst/
│       └── AudioService.cs (UPDATE - add AVAudioSession routing)
│
ViewModels/
└── SettingsViewModel.cs (NEW - or update existing)

Views/
└── AudioSettingsPage.xaml (NEW)
```

---

## Estimated Effort Summary

| Phase | Description | Effort | Priority |
|-------|-------------|--------|----------|
| Phase 1 | Windows full device selection | 4-8 hours | **HIGH** |
| Phase 2 | macOS partial device selection | 8-12 hours | **HIGH** |
| Phase 3 | Dynamic switching & advanced features | 12-20 hours | **MEDIUM** |
| Testing | Comprehensive device testing | 4-6 hours | **HIGH** |
| **TOTAL** | | **28-46 hours** | |

---

## Conclusion

**✅ FEASIBLE:** Independent audio device selection is fully supported on Windows and partially supported on macOS/MacCatalyst.

**Key Takeaways:**
1. **Windows** - Trivial to implement with NAudio's built-in device selection
2. **macOS/MacCatalyst** - Limited to AVAudioSession routing (not full device control)
3. **WebSocket APIs** - No compatibility issues (device selection is client-side)
4. **Bluetooth** - Works but adds latency and requires careful error handling

**Recommendation:** Implement Phase 1 (Windows) first to validate UX, then tackle Phase 2 (macOS) with appropriate platform limitations communicated to users.

---

## References

### Windows (WASAPI/NAudio)
- [NAudio Device Enumeration](https://github.com/naudio/NAudio/wiki/Enumerating-Audio-Devices)
- [Microsoft WASAPI Documentation](https://learn.microsoft.com/en-us/windows/win32/coreaudio/wasapi)

### macOS (Core Audio/AVAudioSession)
- [Apple AVAudioSession Documentation](https://developer.apple.com/documentation/avfaudio/avaudiosession)
- [Apple Core Audio Overview](https://developer.apple.com/library/archive/documentation/MusicAudio/Conceptual/CoreAudioOverview/)
- [Audio MIDI Setup Guide](https://support.apple.com/guide/audio-midi-setup/)

### Cross-Platform
- [PortAudio](http://www.portaudio.com/)
- [.NET MAUI Platform Services](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/)

---

**Document Version:** 1.0  
**Last Updated:** December 23, 2025  
**Next Review:** After Phase 1 implementation


