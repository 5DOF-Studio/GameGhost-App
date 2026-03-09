# Phase 08: Polish - Research

**Researched:** 2026-03-04
**Domain:** .NET MAUI state management, AppKit CALayer effects, capture pipeline parameterization
**Confidence:** HIGH

## Summary

Phase 08 covers five major work areas: (1) Voice Chat behavioral redesign separating connection from microphone activation, (2) Ghost Mode card reskin with indigo glass morphism, (3) Settings UX improvements, (4) Capture pipeline parameterization for multi-agent support, and (5) Code review fixes from the Phase 10 audit.

The Voice Chat redesign is the highest-risk item -- it touches the MainViewModel state machine that orchestrates connection, audio, capture, and ghost mode. The card reskin is isolated to a single Swift file (EventCardView.swift) with no layout changes. The code review fixes are mostly trivial (several already applied). Capture parameterization is medium complexity but well-contained.

**Primary recommendation:** Execute in dependency order: code review fixes first (stabilize foundation), then Voice Chat redesign (highest complexity), then capture parameterization (depends on clean connection semantics), then Settings UX, then Ghost Mode card reskin (fully independent).

## Standard Stack

No new libraries needed. All work uses existing stack:

### Core (Already Present)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET MAUI | 8.0 | Cross-platform UI | Project framework |
| CommunityToolkit.Mvvm | 8.x | ObservableProperty, RelayCommand | MVVM source generators |
| SkiaSharp | 2.88+ | Image processing (FrameDiffService) | Already used for dHash |
| AppKit (native) | macOS 13+ | Ghost mode overlay cards | Pure AppKit constraint |
| Core Animation | macOS 13+ | CAGradientLayer, CALayer effects | Native layer system |

### No New Dependencies
All changes use existing APIs. No NuGet packages or Swift packages to add.

## Architecture Patterns

### Pattern 1: Voice Chat State Separation

**What:** Decouple "connected to API" from "microphone active" into two independent states.

**Current state machine (lines 276-301 of MainViewModel.cs):**
```
User toggles IsMicActive ON
  -> If disconnected: auto-connects (ToggleConnectionAsync)
  -> ConnectionState handler: IsMicActive = (state == Connected)  // FORCED
  -> ToggleConnectionAsync: StartRecordingAsync immediately after ConnectAsync

User toggles IsMicActive OFF
  -> If connected: disconnects entirely (ToggleConnectionAsync)
```

**Target state machine:**
```
"Connect" button -> ConnectAsync (text-only API connection, no mic)
  -> ConnectionState.Connected means API is ready, NOT that mic is on

"Voice Chat" toggle (replaces MIC toggle) -> IsVoiceChatActive
  -> ON: StartRecordingAsync + initialize live conversational API
  -> OFF: StopRecordingAsync + tear down live session
  -> Independent of connection state (but requires connection)

Disconnect -> tears down everything (voice chat + connection)
```

**Key changes required in MainViewModel.cs:**

1. **Line 278:** Remove `IsMicActive = state == ConnectionState.Connected;` -- this auto-forced mic on connection.

2. **Lines 496-524 (HandleMicToggleAsync):** This entire method needs redesign. Currently IsMicActive drives connection. New property IsVoiceChatActive should drive mic only when already connected.

3. **Lines 648-664 (ToggleConnectionAsync connect branch):** Remove `StartRecordingAsync` from this method. Audio capture should only start when Voice Chat is toggled ON independently.

4. **Lines 715-718 (StopSessionAsync):** Keep IsMicActive/IsVoiceChatActive reset on disconnect -- this is cleanup, not auto-activation.

5. **New property:** Add `[ObservableProperty] private bool _isVoiceChatActive;` with a partial change handler that starts/stops mic recording.

6. **Ghost mode sync:** `SyncAudioStateToGhost()` currently references IsMicActive. Update to reference IsVoiceChatActive.

**Risk:** HIGH. This touches the core state machine. Every test that checks connection + mic behavior needs updating. The connector card toggle binding (MainPage.xaml line 411) must be rebound.

### Pattern 2: NSVisualEffectView Glass Blur (AppKit)

**What:** Add native macOS glass blur behind EventCardNSView using NSVisualEffectView.

**How (verified from AppKit API):**
```swift
// In EventCardNSView.init(), BEFORE setting layer properties:

// 1. Create blur effect view as background
let blurView = NSVisualEffectView(frame: bounds)
blurView.blendingMode = .behindWindow      // Blurs content behind the window
blurView.material = .hudWindow             // Dark translucent material (matches gamer aesthetic)
blurView.state = .active                   // Always active (don't follow window state)
blurView.autoresizingMask = [.width, .height]
blurView.wantsLayer = true
blurView.layer?.cornerRadius = 12
blurView.layer?.masksToBounds = true
addSubview(blurView, positioned: .below, relativeTo: subviews.first)

// 2. Increase card background opacity
// Change from 0.55 to 0.85 (line 21)
private let cardBg = NSColor(red: 30/255, green: 30/255, blue: 46/255, alpha: 0.85)
```

**Constraint:** This is pure AppKit. NSVisualEffectView is available on macOS 10.10+. `.behindWindow` blending mode shows the desktop/content behind the ghost overlay panel. The `.hudWindow` material gives a dark, translucent effect ideal for the sci-fi aesthetic.

**Confidence:** HIGH -- NSVisualEffectView is stable, well-documented AppKit API.

### Pattern 3: CAGradientLayer Border Mask (AppKit)

**What:** Replace plain shadow borders with gradient indigo borders on event cards.

**How:**
```swift
// Add gradient border using CAGradientLayer as a mask
private func addGradientBorder(to view: NSView, cornerRadius: CGFloat = 12) {
    let borderWidth: CGFloat = 1.5

    // Gradient layer for the border
    let gradientLayer = CAGradientLayer()
    gradientLayer.frame = view.bounds
    gradientLayer.colors = [
        NSColor(red: 0x4F/255, green: 0x46/255, blue: 0xE5/255, alpha: 1).cgColor,  // #4F46E5
        NSColor(red: 0x63/255, green: 0x66/255, blue: 0xF1/255, alpha: 1).cgColor,  // #6366F1
        NSColor(red: 0x81/255, green: 0x8C/255, blue: 0xF8/255, alpha: 1).cgColor,  // #818CF8
    ]
    gradientLayer.startPoint = CGPoint(x: 0, y: 0)
    gradientLayer.endPoint = CGPoint(x: 1, y: 1)
    gradientLayer.cornerRadius = cornerRadius

    // Shape mask to create border-only effect (hollow center)
    let maskLayer = CAShapeLayer()
    let outerPath = CGPath(roundedRect: view.bounds,
                           cornerWidth: cornerRadius, cornerHeight: cornerRadius,
                           transform: nil)
    let innerRect = view.bounds.insetBy(dx: borderWidth, dy: borderWidth)
    let innerPath = CGPath(roundedRect: innerRect,
                           cornerWidth: cornerRadius - borderWidth,
                           cornerHeight: cornerRadius - borderWidth,
                           transform: nil)
    let combined = CGMutablePath()
    combined.addPath(outerPath)
    combined.addPath(innerPath)
    maskLayer.path = combined
    maskLayer.fillRule = .evenOdd
    gradientLayer.mask = maskLayer

    view.layer?.addSublayer(gradientLayer)
}
```

**Inner glow pattern:**
```swift
// Add subtle inner glow using a radial gradient sublayer
let glowLayer = CAGradientLayer()
glowLayer.type = .radial
glowLayer.frame = view.bounds
glowLayer.colors = [
    NSColor(red: 0x81/255, green: 0x8C/255, blue: 0xF8/255, alpha: 0.15).cgColor,
    NSColor.clear.cgColor,
]
glowLayer.startPoint = CGPoint(x: 0.5, y: 0)
glowLayer.endPoint = CGPoint(x: 1, y: 1)
glowLayer.cornerRadius = 12
view.layer?.insertSublayer(glowLayer, at: 0)
```

**Gradient dividers:**
```swift
// Replace solid dividers with gradient transparent -> white/30% -> transparent
private func addGradientDivider(at y: CGFloat, padding: CGFloat, width: CGFloat) -> CGFloat {
    let divider = NSView(frame: NSRect(x: padding, y: y, width: width, height: 1))
    divider.wantsLayer = true

    let gradient = CAGradientLayer()
    gradient.frame = divider.bounds
    gradient.colors = [
        NSColor.clear.cgColor,
        NSColor.white.withAlphaComponent(0.3).cgColor,
        NSColor.clear.cgColor,
    ]
    gradient.startPoint = CGPoint(x: 0, y: 0.5)
    gradient.endPoint = CGPoint(x: 1, y: 0.5)
    divider.layer?.addSublayer(gradient)

    addSubview(divider)
    return y + 1 + 8
}
```

**Confidence:** HIGH -- CAGradientLayer and CAShapeLayer are stable Core Animation APIs, widely used in AppKit.

### Pattern 4: Capture Config Parameterization

**What:** Extract hardcoded capture values into a record on Agent, pass to services.

**Current hardcoded values:**
- `WindowCaptureService.CaptureIntervalMs` = 30000 (macOS) / 1000 (Windows)
- `FrameDiffService._debounceWindow` = 1.5s
- `FrameDiffService.HasChanged(threshold=10)` default

**Design:**
```csharp
// New record on Agent or as standalone model
public record CaptureConfig(
    int CaptureIntervalMs = 30000,
    int DiffThreshold = 10,
    double DebounceWindowSeconds = 1.5
);

// Add to Agent model
public CaptureConfig? CaptureConfig { get; init; }
```

**Interface change for IWindowCaptureService:**
```csharp
// Option A: Add interval parameter to StartCaptureAsync
Task StartCaptureAsync(CaptureTarget target, int captureIntervalMs = 30000);

// Option B: Property setter (less breaking)
int CaptureIntervalMs { get; set; }
```

**Option A is cleaner** -- pass config at start time. The WindowCaptureService implementations already create the timer in StartCaptureAsync, so the interval can be a parameter instead of a const.

**FrameDiffService needs constructor injection:**
```csharp
public sealed class FrameDiffService : IFrameDiffService
{
    private readonly TimeSpan _debounceWindow;
    private readonly int _defaultThreshold;

    public FrameDiffService(TimeSpan? debounceWindow = null, int defaultThreshold = 10)
    {
        _debounceWindow = debounceWindow ?? TimeSpan.FromSeconds(1.5);
        _defaultThreshold = defaultThreshold;
    }
}
```

**MainViewModel capture lambda (lines 208-258):** Extract to a method, pass config from SelectedAgent.

**Confidence:** HIGH -- straightforward parameter extraction, no new patterns.

### Anti-Patterns to Avoid
- **Coupling Voice Chat to Connection state:** The whole point of this redesign is to decouple them. Do not re-introduce any `IsMicActive = IsConnected` semantics.
- **Modifying EventCardView layout:** The card reskin is COLOR/STYLE ONLY. Do not change frame sizes, padding, or subview hierarchy.
- **Breaking the brain pipeline:** Capture parameterization must not alter the brain pipeline rules. Brain remains sole image consumer.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Glass blur | Custom blur shader | NSVisualEffectView | Native macOS API, GPU-accelerated, respects system settings |
| Gradient borders | Manual pixel drawing | CAGradientLayer + CAShapeLayer mask | Core Animation handles rendering, animation, and hit testing |
| State machine | Complex bool flags | CommunityToolkit ObservableProperty | Source-generated change handlers, thread-safe UI binding |

## Common Pitfalls

### Pitfall 1: Voice Chat Toggle Creates Race Condition with Connection
**What goes wrong:** If IsVoiceChatActive is toggled ON before connection completes, StartRecordingAsync may fire against a not-yet-ready API.
**Why it happens:** ConnectionState transitions are async; UI toggles are immediate.
**How to avoid:** Guard IsVoiceChatActive handler with `if (ConnectionState != ConnectionState.Connected) return;`. Disable the Voice Chat toggle in UI when not connected.
**Warning signs:** Tests where toggling voice chat before connection passes but produces no audio.

### Pitfall 2: NSVisualEffectView Frame Not Updated on Card Resize
**What goes wrong:** Blur view stays at initial frame size when card content causes height change.
**Why it happens:** EventCardNSView calculates its frame size dynamically in buildTextCard/buildTextWithImageCard. NSVisualEffectView created in init has frame .zero.
**How to avoid:** Set `blurView.autoresizingMask = [.width, .height]` AND call `blurView.frame = bounds` after final frame.size is set. Or update in layout().
**Warning signs:** Blur appears only at top of card, or card has transparent bottom.

### Pitfall 3: CAGradientLayer Frame Not Matching After Resize
**What goes wrong:** Gradient border or glow appears wrong size or misaligned.
**Why it happens:** CALayer.frame is set once during init but the view's bounds change when content is laid out.
**How to avoid:** Override `layout()` in EventCardNSView to update all gradient/glow sublayer frames to match current bounds.
**Warning signs:** Gradient border visible but offset or clipped.

### Pitfall 4: Settings Provider Change Leaves Stale Provider in DI
**What goes wrong:** User changes voice provider in settings, but the DI-registered IConversationProvider is still the old one.
**Why it happens:** ConversationProviderFactory creates the provider at DI registration time, not per-request.
**How to avoid:** The factory pattern already handles this -- ConversationProviderFactory.Create() is called when connecting. Verify the factory reads current settings at Create() time, not cached from startup.
**Warning signs:** After changing provider in settings, reconnecting uses old provider.

### Pitfall 5: Ghost Mode Rebuild Required After Card Reskin
**What goes wrong:** Card changes don't appear because the xcframework wasn't rebuilt.
**Why it happens:** EventCardView.swift is in the native GaimerGhostMode Swift package, which must be compiled to xcframework separately.
**How to avoid:** After modifying EventCardView.swift, rebuild the xcframework using the existing build script, then copy to the project.
**Warning signs:** Changes compile fine but ghost mode cards look unchanged at runtime.

## Code Review Fixes -- Current Status

Several fixes from the Phase 10 audit have ALREADY been applied. Here is the current status:

### Already Fixed (verified in codebase)
| Fix | Status | Evidence |
|-----|--------|----------|
| Fix 1: SupabaseAuthService IDisposable | DONE | Line 9: `class SupabaseAuthService : IAuthService, IDisposable`, line 124: `public void Dispose()` |
| Fix 2: Console.WriteLine logging | DONE | All auth logging uses `System.Diagnostics.Debug.WriteLine` |
| Fix 3: OpenAI APIKEY fallback | DONE | Lines 58-61 only have `OPENAI_APIKEY`, `OPENAI_API_KEY`, `OpenAiApiKey` -- no bare `APIKEY` |
| Fix 4: Shell.Current null guards | PARTIAL | Lines 617, 670, 751, 771, 790, 1241 all use `Shell.Current?.` or `Shell.Current is not null` |
| Fix 7: SettingsService cached GUID | DONE | Line 16: `private string? _fallbackDeviceId;`, line 99: `_fallbackDeviceId ??= Guid.NewGuid()` |

### Still Open
| Fix | Status | What Remains |
|-----|--------|--------------|
| Fix 4 (partial): Disconnect test NREs | OPEN | 7 tests catch NRE from Shell.Current in disconnect paths -- need mock/guard in test setup |
| Fix 5: Disconnecting state delegation | OPEN | GeminiConversationProvider/OpenAIConversationProvider state delegation issue |
| Fix 6: PipelineIntegrationTests Task.Delay | OPEN | Line 59: `await Task.Delay(1500)` -- replace with polling loop |
| Fix 8: SupabaseAuthService tests call-counter | OPEN | Lines 184-188, 226-230 use call-counter pattern instead of URL inspection |
| Console.WriteLine in non-auth code | OPEN | MainViewModel line 206, line 631 still use Console.WriteLine |

## Settings UX Implementation

### Gear Icon in MainPage Top Bar
The MainPage.xaml does not currently have a top bar navigation area. The gear icon should be placed in the top-right area of the main layout. Use an ImageButton with a gear SF Symbol or PNG icon.

```xml
<ImageButton Source="gear.png"
             Command="{Binding NavigateToSettingsCommand}"
             WidthRequest="28"
             HeightRequest="28"
             VerticalOptions="Center"/>
```

Add a `[RelayCommand] NavigateToSettings` in MainViewModel that navigates to the existing "Settings" route.

### Session Restart on Provider Change
The SettingsPage needs to:
1. Track whether voice provider or gender changed during the session
2. On save: if changed AND currently connected, show confirmation dialog
3. On confirm: disconnect current session, navigate back, auto-reconnect with new config

The ConversationProviderFactory already reads settings at Create() time, so a disconnect + reconnect will pick up new settings automatically.

## Dependency Map Between Items

```
Code Review Fixes (independent, do first)
    |
Voice Chat Redesign (touches MainViewModel state machine)
    |
Capture Parameterization (uses clean connection semantics from Voice Chat)
    |
Settings UX (uses disconnect/reconnect flow)

Ghost Mode Card Reskin (fully independent -- can run in parallel with anything)
```

Items with NO dependencies on each other:
- Ghost Mode card reskin
- Code review fixes (Fix 5, 6, 8)
- Console.WriteLine cleanup

Items that MUST be sequenced:
- Voice Chat redesign BEFORE capture parameterization (both touch MainViewModel connection flow)
- Voice Chat redesign BEFORE Settings UX (settings restart uses disconnect flow)

## Open Questions

1. **Should IsMicActive be renamed to IsVoiceChatActive or should both properties coexist?**
   - What we know: IsMicActive is bound in MainPage.xaml (line 411) and referenced in ghost mode sync. Renaming is cleaner but more files to touch.
   - Recommendation: Rename IsMicActive to IsVoiceChatActive throughout. The old name is misleading in the new architecture. One-time cost, long-term clarity.

2. **How does the Voice Chat toggle interact with the existing connector card toggle?**
   - What we know: MainPage.xaml line 410-411 has a HorizontalIndustrialToggle bound to IsMicActive. This is in the "Connectors" section, next to the game target.
   - Recommendation: This toggle becomes the "Connect" toggle (bound to a new ToggleConnectionCommand). A separate Voice Chat toggle appears after connection is established.

3. **Audio visualizer (SkiaSharp) -- scope unclear**
   - What we know: Listed as original Phase 08 item but no spec or design.
   - Recommendation: Defer or scope to a simple waveform bar using existing InputVolume/OutputVolume float values. SkiaSharp is already in the project.

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection of MainViewModel.cs (892 lines), EventCardView.swift (374 lines), FrameDiffService.cs, WindowCaptureService.cs (macOS + Windows), Agent.cs, MainPage.xaml, SettingsService.cs, SupabaseAuthService.cs, OpenAIRealtimeService.cs, AppShell.xaml.cs
- Apple NSVisualEffectView documentation (stable API since macOS 10.10)
- Core Animation CAGradientLayer/CAShapeLayer (stable API since macOS 10.5)
- CommunityToolkit.Mvvm ObservableProperty pattern (used throughout codebase)

### Secondary (MEDIUM confidence)
- NSVisualEffectView `.behindWindow` + `.hudWindow` material combination for dark glass effect -- commonly used pattern in macOS menu bar apps and HUD overlays

## Metadata

**Confidence breakdown:**
- Voice Chat redesign: HIGH -- full state machine traced through code, all touch points identified
- Ghost Mode card reskin: HIGH -- pure AppKit/CA APIs, no unknowns
- Code review fixes: HIGH -- each fix verified against current code, status confirmed
- Capture parameterization: HIGH -- straightforward parameter extraction, interfaces well-defined
- Settings UX: HIGH -- existing Shell routing, factory pattern handles provider switching

**Research date:** 2026-03-04
**Valid until:** 2026-04-04 (stable -- no fast-moving dependencies)
