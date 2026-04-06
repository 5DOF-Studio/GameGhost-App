# Screen Capture Architecture

**Project:** Gaimer Desktop (.NET MAUI)  
**Status:** Architecture Proposal — Pending Implementation  
**Created:** February 24, 2026  
**Related:** `IWindowCaptureService.cs`, `MockWindowCaptureService.cs`, `MainViewModel.cs`

---

## Table of Contents

1. [Overview](#1-overview)
2. [Current State](#2-current-state)
3. [Architecture Design](#3-architecture-design)
4. [Platform Implementations](#4-platform-implementations)
5. [Integration Points](#5-integration-points)
6. [Implementation Plan](#6-implementation-plan)

---

## 1. Overview

The screen capture system enables Gaimer to watch gameplay in real-time by capturing screenshots of target application windows. These frames are then sent to the Brain (via conversation providers) for analysis.

### Requirements

| Requirement | Description |
|-------------|-------------|
| Window Enumeration | List all visible application windows with process name, title |
| Window Thumbnails | Generate 64×64 thumbnails for window selector UI |
| Frame Capture | Capture window contents at configurable FPS (default 1 FPS) |
| JPEG Compression | Compress frames to JPEG (60% quality) for bandwidth |
| Scaling | Scale captured images to 50% for faster AI processing |
| Window Tracking | Handle window closure, minimization, focus changes |
| Cross-Platform | Support Windows 10+ and macOS (MacCatalyst) |

---

## 2. Current State

### Interface Definition

```csharp
// Services/IWindowCaptureService.cs
public interface IWindowCaptureService
{
    event EventHandler<byte[]>? FrameCaptured;

    Task<IReadOnlyList<CaptureTarget>> GetCaptureTargetsAsync();
    Task StartCaptureAsync(CaptureTarget target);
    Task StopCaptureAsync();
    bool IsCapturing { get; }
    CaptureTarget? CurrentTarget { get; }
}
```

### CaptureTarget Model

```csharp
// Models/CaptureTarget.cs
public partial class CaptureTarget : ObservableObject
{
    public required nint Handle { get; init; }      // Platform window handle
    public required string ProcessName { get; init; }
    public required string WindowTitle { get; init; }
    public byte[]? Thumbnail { get; set; }          // 64×64 preview
    public string? ChessBadge { get; set; }         // Game detection badge
    public bool IsDisabled { get; set; }            // Uncapturable (minimized, etc.)
    
    [ObservableProperty]
    private bool _isSelected;
}
```

### Current Integration (MainViewModel)

```csharp
// FrameCaptured handler already wired
_captureService.FrameCaptured += (_, frame) =>
{
    PreviewImage = frame;
    // ... visual reel + brain event router calls
    if (_conversationProvider.IsConnected)
    {
        _ = _conversationProvider.SendImageAsync(frame);
    }
};
```

---

## 3. Architecture Design

### Layer Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                      MainViewModel                          │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  FrameCaptured → PreviewImage + BrainRouter + AI    │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  IWindowCaptureService                      │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  GetCaptureTargetsAsync() → List<CaptureTarget>     │   │
│  │  StartCaptureAsync(target) → Timer(1 FPS)           │   │
│  │  StopCaptureAsync() → Cleanup                       │   │
│  │  FrameCaptured event → byte[] (JPEG)                │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│    Windows      │ │   MacCatalyst   │ │      Mock       │
│ WindowCapture   │ │ WindowCapture   │ │ WindowCapture   │
│    Service      │ │    Service      │ │    Service      │
├─────────────────┤ ├─────────────────┤ ├─────────────────┤
│ EnumWindows     │ │ CGWindowList    │ │ Static data     │
│ PrintWindow     │ │ CGWindowCreate  │ │ Placeholder img │
│ DWMAPI (opt)    │ │ ImageRef        │ │                 │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

### Capture Pipeline

```
Window Handle → Capture API → Raw Bitmap → Scale (50%) → JPEG (60%) → FrameCaptured event
      │              │             │            │             │              │
   nint/int     Platform      byte[] RGB    Resize      Compress        Fire event
                  API          pixels       dims         to bytes
```

---

## 4. Platform Implementations

### 4.1 Windows Implementation

**File:** `Platforms/Windows/WindowCaptureService.cs`

**APIs Used:**
| API | Purpose |
|-----|---------|
| `User32.EnumWindows` | List visible windows |
| `User32.GetWindowText` | Get window title |
| `User32.GetClassName` | Filter system windows |
| `User32.PrintWindow` | Capture window bitmap |
| `Dwmapi.DwmGetWindowAttribute` | Get DWM cloaked state |
| `System.Drawing` | Bitmap manipulation |

**P/Invoke Signatures:**

```csharp
[DllImport("user32.dll")]
private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

[DllImport("user32.dll")]
private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

[DllImport("dwmapi.dll")]
private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
```

**Window Enumeration Logic:**

```csharp
public async Task<IReadOnlyList<CaptureTarget>> GetCaptureTargetsAsync()
{
    var targets = new List<CaptureTarget>();
    
    EnumWindows((hWnd, _) =>
    {
        // Skip invisible, minimized, or cloaked windows
        if (!IsWindowVisible(hWnd)) return true;
        if (IsIconic(hWnd)) return true;
        if (IsCloaked(hWnd)) return true;
        
        // Skip system classes
        var className = GetClassName(hWnd);
        if (IsSystemWindow(className)) return true;
        
        // Get window info
        var title = GetWindowText(hWnd);
        var processName = GetProcessName(hWnd);
        
        if (string.IsNullOrWhiteSpace(title)) return true;
        
        var target = new CaptureTarget
        {
            Handle = hWnd,
            ProcessName = processName,
            WindowTitle = title,
            ChessBadge = DetectChessBadge(title, processName),
            Thumbnail = CaptureWindowThumbnail(hWnd, 64, 64),
        };
        targets.Add(target);
        
        return true; // Continue enumeration
    }, IntPtr.Zero);
    
    return targets;
}

private static string[] SystemClasses = 
{
    "Shell_TrayWnd",       // Taskbar
    "Progman",             // Desktop
    "WorkerW",             // Desktop layers
    "Windows.UI.Core.CoreWindow", // UWP system windows
    "ApplicationFrameWindow",     // Some UWP hosts
};
```

**Frame Capture Logic:**

```csharp
private byte[] CaptureWindow(IntPtr hWnd)
{
    // Get window dimensions
    GetWindowRect(hWnd, out RECT rect);
    var width = rect.Right - rect.Left;
    var height = rect.Bottom - rect.Top;
    
    if (width <= 0 || height <= 0) return Array.Empty<byte>();
    
    // Create bitmap
    using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
    using var graphics = Graphics.FromImage(bitmap);
    var hdc = graphics.GetHdc();
    
    try
    {
        // PW_RENDERFULLCONTENT = 0x02 (captures even when window is obscured)
        PrintWindow(hWnd, hdc, 0x02);
    }
    finally
    {
        graphics.ReleaseHdc(hdc);
    }
    
    // Scale to 50%
    var scaledWidth = width / 2;
    var scaledHeight = height / 2;
    using var scaled = new Bitmap(scaledWidth, scaledHeight);
    using var scaleGraphics = Graphics.FromImage(scaled);
    scaleGraphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
    scaleGraphics.DrawImage(bitmap, 0, 0, scaledWidth, scaledHeight);
    
    // Compress to JPEG
    using var stream = new MemoryStream();
    var encoder = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
    var encoderParams = new EncoderParameters(1);
    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 60L);
    scaled.Save(stream, encoder, encoderParams);
    
    return stream.ToArray();
}
```

### 4.2 macOS (MacCatalyst) Implementation

**File:** `Platforms/MacCatalyst/WindowCaptureService.cs`

**APIs Used:**
| API | Purpose |
|-----|---------|
| `CGWindowListCopyWindowInfo` | Enumerate windows |
| `CGWindowListCreateImage` | Capture window |
| `NSImage` / `CGImage` | Image manipulation |

**Binding Approach:** Use ObjC runtime via `ObjCRuntime.Messaging` or create Swift wrapper.

**Window Enumeration (Conceptual):**

```csharp
public async Task<IReadOnlyList<CaptureTarget>> GetCaptureTargetsAsync()
{
    var targets = new List<CaptureTarget>();
    
    // kCGWindowListOptionOnScreenOnly | kCGWindowListExcludeDesktopElements
    var windowList = CGWindowListCopyWindowInfo(
        CGWindowListOption.OnScreenOnly | CGWindowListOption.ExcludeDesktopElements,
        CGWindowID.Null // kCGNullWindowID
    );
    
    foreach (var windowInfo in windowList)
    {
        // Skip windows without names
        var name = windowInfo["kCGWindowOwnerName"];
        var title = windowInfo["kCGWindowName"];
        var windowNumber = windowInfo["kCGWindowNumber"];
        var layer = windowInfo["kCGWindowLayer"];
        
        // Skip system layers (negative or >0 typically system)
        if (layer < 0 || layer > 100) continue;
        
        // Skip small windows
        var bounds = windowInfo["kCGWindowBounds"];
        if (bounds.Width < 100 || bounds.Height < 100) continue;
        
        var target = new CaptureTarget
        {
            Handle = (nint)windowNumber,
            ProcessName = name,
            WindowTitle = title ?? name,
            ChessBadge = DetectChessBadge(title ?? name, name),
            Thumbnail = await CaptureWindowThumbnailAsync(windowNumber, 64, 64),
        };
        targets.Add(target);
    }
    
    return targets;
}
```

**Frame Capture (Conceptual):**

```csharp
private byte[] CaptureWindow(uint windowNumber)
{
    // CGWindowListCreateImage with specific window
    var windowArray = new uint[] { windowNumber };
    var cgImage = CGWindowListCreateImage(
        CGRect.Null,  // Full bounds
        CGWindowListOption.IncludingWindow,
        windowNumber,
        CGWindowImageOption.BoundsIgnoreFraming
    );
    
    if (cgImage == IntPtr.Zero) return Array.Empty<byte>();
    
    // Convert to NSImage, scale, then to JPEG
    using var nsImage = new NSImage(cgImage, CGSize.Empty);
    var scaledImage = ScaleImage(nsImage, 0.5);
    var jpegData = scaledImage.RepresentationUsingType(NSBitmapImageFileType.Jpeg, 
        new NSDictionary(NSImageCompressionFactor, 0.6));
    
    return jpegData.ToArray();
}
```

**Important macOS Notes:**
- **Screen Recording Permission Required:** App must request `com.apple.security.screen-recording` entitlement
- **User Consent:** First call triggers system permission dialog
- **Sandboxing:** Must be handled carefully; may need hardened runtime
- Add to `Entitlements.plist`:
  ```xml
  <key>com.apple.security.app-sandbox</key>
  <true/>
  <key>com.apple.security.screen-recording</key>
  <true/>
  ```

---

## 5. Integration Points

### 5.1 DI Registration (MauiProgram.cs)

```csharp
#if WINDOWS
builder.Services.AddSingleton<IWindowCaptureService, WindowCaptureService>();
#elif MACCATALYST
builder.Services.AddSingleton<IWindowCaptureService, WindowCaptureService>();
#else
builder.Services.AddSingleton<IWindowCaptureService, MockWindowCaptureService>();
#endif
```

### 5.2 MainViewModel Integration (Already Done)

The `MainViewModel` already handles:
- `FrameCaptured` event subscription
- Setting `PreviewImage` for UI
- Calling `_visualReelService.Append(moment)` for tracking
- Calling `_brainEventRouter.OnScreenCapture(...)` for timeline
- Sending to `_conversationProvider.SendImageAsync(frame)` for AI

### 5.3 Game Detection Logic

```csharp
private static string? DetectChessBadge(string title, string processName)
{
    var combined = $"{title} {processName}".ToLowerInvariant();
    
    if (combined.Contains("chess.com"))
        return "♟️ Chess.com";
    if (combined.Contains("lichess"))
        return "♟️ Lichess";
    if (combined.Contains("chessbase"))
        return "♟️ ChessBase";
    
    // Future: Add more game detections
    // if (combined.Contains("valorant")) return "🎯 Valorant";
    // if (combined.Contains("fortnite")) return "🎮 Fortnite";
    
    return null;
}
```

---

## 6. Implementation Plan

### Phase 1: Windows (Est. 8-12 hours)

| Task | Hours | Priority |
|------|-------|----------|
| P/Invoke declarations | 2 | P0 |
| Window enumeration | 2 | P0 |
| System window filtering | 1 | P0 |
| PrintWindow capture | 2 | P0 |
| Scaling + JPEG compression | 1 | P0 |
| Thumbnail generation | 1 | P1 |
| Window closure detection | 1 | P1 |
| Testing | 2 | P0 |

### Phase 2: macOS (Est. 12-16 hours)

| Task | Hours | Priority |
|------|-------|----------|
| ObjC bindings / Swift wrapper | 4 | P0 |
| CGWindowListCopyWindowInfo | 2 | P0 |
| CGWindowListCreateImage | 2 | P0 |
| Permission handling | 2 | P0 |
| Image scaling + JPEG | 2 | P0 |
| Entitlements setup | 1 | P0 |
| Testing | 3 | P0 |

### Phase 3: Polish (Est. 4-6 hours)

| Task | Hours | Priority |
|------|-------|----------|
| Configurable FPS | 1 | P2 |
| Capture quality settings | 1 | P2 |
| Error handling (window gone) | 2 | P1 |
| Performance optimization | 2 | P2 |

### Dependencies

- **Windows:** `System.Drawing.Common` (already available)
- **macOS:** May need NuGet for CoreGraphics bindings or custom Swift interop

---

## Appendix: Window Filtering Heuristics

### Windows - Skip These Classes:
- `Shell_TrayWnd` (Taskbar)
- `Progman` (Desktop)
- `WorkerW` (Desktop layers)
- `DV2ControlHost` (Start menu)
- `Windows.UI.Core.CoreWindow` (certain UWP)
- `EdgeUiInputTopWndClass` (Edge UI)

### Windows - Skip These Process Names:
- `explorer.exe` (unless visible window with title)
- `SearchUI.exe`
- `TextInputHost.exe`
- `StartMenuExperienceHost.exe`

### macOS - Skip These Layer Values:
- Layer < 0 (system underlays)
- Layer > 100 (notification center, etc.)

### Both Platforms - Skip:
- Windows with empty or whitespace-only titles
- Windows smaller than 100×100 pixels
- Minimized windows

---

**Next Steps:** This architecture document is ready for implementation. Begin with Windows (more straightforward P/Invoke) before tackling macOS (requires more complex bindings).
