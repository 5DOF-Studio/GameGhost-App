# Witness AI - .NET MAUI Desktop Specification

**Version:** 2.0.0  
**Date:** December 9, 2024  
**Purpose:** Desktop-First Implementation Guide (.NET MAUI)  
**Target Platforms:** Windows 10+, macOS 12+

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Why .NET MAUI for Desktop](#2-why-net-maui-for-desktop)
3. [Architecture Overview](#3-architecture-overview)
4. [Window Capture System](#4-window-capture-system)
5. [Audio System Design](#5-audio-system-design)
6. [Network Layer](#6-network-layer)
7. [UI/UX Implementation](#7-uiux-implementation)
8. [Platform Services](#8-platform-services)
9. [Error Handling & Recovery](#9-error-handling--recovery)
10. [Build & Distribution](#10-build--distribution)
11. [Implementation Roadmap](#11-implementation-roadmap)

---

## 1. Executive Summary

### 1.1 Project Scope

**Witness Desktop** is a desktop-first AI companion application using .NET MAUI that enables users to:
- Have voice conversations with an AI assistant
- Share a specific application window for visual analysis
- Receive contextual commentary on games, code, or any visual content

**Target Platforms:**
- **Windows 10/11** (x64, ARM64)
- **macOS 12+** (Intel, Apple Silicon)

### 1.2 Key Differentiators from Web Version

| Feature | Web Version | Desktop Version |
|---------|-------------|-----------------|
| Video Input | Camera + Screen share | **App window capture only** |
| Audio Input | Microphone | Microphone |
| Audio Output | AI speech | AI speech |
| App Audio Capture | N/A | **Future v2.0** |
| Platform | Browser | Native desktop |

### 1.3 Design Philosophy

**Image-First Capture (v1.0):**
- Capture screenshots of a user-selected application window
- Default behavior is **local Visual Reel** storage (short-retention, time-indexed), enabling fast “check what happened X minutes ago” without continuous model calls
- Optional **Live / High Attention** mode (explicit user toggle) continuously processes the latest captured frames for near-real-time feedback
- No camera support (privacy-focused)
- No app audio capture (simplified architecture)
- User voice + (selective) window images sent to Gemini API (on-demand by default; continuous only in Live mode)

### 1.4 Key Performance Targets

| Metric | Target | Implementation |
|--------|--------|----------------|
| Audio Latency (Input) | <50ms | WASAPI/CoreAudio |
| Audio Latency (Output) | <50ms | Native buffers |
| Window Capture Rate (Baseline) | ~1 FPS | Visual Reel capture (cheap default) |
| Window Capture Rate (Live / High Attention) | 2–3 FPS | Drop-frame/latest-frame-wins + diff-gating + downscaled transmit |
| Vision Loop Latency (Live) | <2s ideal | WebSocket + bounded frame queue (no backlog) |
| WebSocket Latency | <20ms | Native HttpClient |
| Memory Footprint | <150MB idle | .NET 8 optimizations |
| CPU Usage | <10% idle | Efficient capture APIs |

---

## 2. Why .NET MAUI for Desktop

### 2.1 Advantages for Window Capture

| Strength | Benefit |
|----------|---------|
| **Native API Access** | Direct P/Invoke to Windows Graphics Capture, CGWindowList |
| **Desktop Heritage** | 20+ years of .NET desktop experience |
| **NuGet Ecosystem** | NAudio for audio, SharpDX for graphics |
| **Single Language** | C# across all layers |
| **Performance** | .NET 8 with native compilation |

### 2.2 Platform-Specific APIs Used

| Platform | Window Capture | Audio |
|----------|----------------|-------|
| Windows | Windows.Graphics.Capture API | WASAPI via NAudio |
| macOS | CGWindowListCreateImage | AVAudioEngine |

---

## 3. Architecture Overview

### 3.1 High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                   WITNESS DESKTOP APPLICATION                     │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                         UI LAYER                             │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │ │
│  │  │ MainPage    │  │ AppSelector │  │ Visualizer  │         │ │
│  │  │ (XAML)      │  │ (XAML)      │  │ (SkiaSharp) │         │ │
│  │  └─────────────┘  └─────────────┘  └─────────────┘         │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                              │                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                      VIEWMODEL LAYER                         │ │
│  │  ┌─────────────────────────────────────────────────────┐   │ │
│  │  │               MainViewModel (MVVM)                   │   │ │
│  │  │  - ConnectionState    - SelectedTarget              │   │ │
│  │  │  - Volume            - CaptureTargets               │   │ │
│  │  └─────────────────────────────────────────────────────┘   │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                              │                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                     SERVICE LAYER                            │ │
│  │  ┌───────────────┐ ┌───────────────┐ ┌───────────────┐     │ │
│  │  │ IAudioService │ │IWindowCapture │ │ IGeminiService│     │ │
│  │  │ (Mic + Play)  │ │   Service     │ │  (WebSocket)  │     │ │
│  │  └───────────────┘ └───────────────┘ └───────────────┘     │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                              │                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │              PLATFORM IMPLEMENTATIONS                        │ │
│  │  ┌──────────────────────┐  ┌──────────────────────┐        │ │
│  │  │      Windows         │  │       macOS          │        │ │
│  │  │  - WASAPI Audio      │  │  - AVAudioEngine     │        │ │
│  │  │  - Graphics Capture  │  │  - CGWindowList      │        │ │
│  │  └──────────────────────┘  └──────────────────────┘        │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
                              ↕
                    ┌──────────────────┐
                    │  Gemini Live API │
                    │    (WebSocket)   │
                    └──────────────────┘
```

### 3.2 Project Structure

```
GaimerDesktop/
├── GaimerDesktop.sln
│
├── src/
│   ├── GaimerDesktop/                    # MAUI Project
│   │   ├── App.xaml(.cs)                  # Application entry
│   │   ├── AppShell.xaml(.cs)             # Navigation shell
│   │   ├── MauiProgram.cs                 # DI container setup
│   │   │
│   │   ├── Views/
│   │   │   ├── MainPage.xaml(.cs)         # Main interface
│   │   │   ├── AppSelectorPopup.xaml(.cs) # Window picker
│   │   │   └── SettingsPage.xaml(.cs)     # Configuration
│   │   │
│   │   ├── ViewModels/
│   │   │   ├── BaseViewModel.cs           # INotifyPropertyChanged
│   │   │   ├── MainViewModel.cs           # Main logic
│   │   │   └── AppSelectorViewModel.cs    # Window selection
│   │   │
│   │   ├── Services/
│   │   │   ├── Interfaces/
│   │   │   │   ├── IAudioService.cs
│   │   │   │   ├── IWindowCaptureService.cs
│   │   │   │   └── IGeminiService.cs
│   │   │   │
│   │   │   └── GeminiLiveService.cs       # WebSocket client
│   │   │
│   │   ├── Models/
│   │   │   ├── ConnectionState.cs
│   │   │   ├── CaptureTarget.cs
│   │   │   └── AudioChunk.cs
│   │   │
│   │   ├── Platforms/
│   │   │   ├── Windows/
│   │   │   │   ├── AudioService.cs        # WASAPI
│   │   │   │   ├── WindowCaptureService.cs
│   │   │   │   └── NativeMethods.cs       # P/Invoke
│   │   │   │
│   │   │   └── MacCatalyst/
│   │   │       ├── AudioService.cs        # AVAudioEngine
│   │   │       └── WindowCaptureService.cs
│   │   │
│   │   ├── Controls/
│   │   │   └── AudioVisualizer.cs         # SkiaSharp canvas
│   │   │
│   │   ├── Utilities/
│   │   │   ├── PcmConverter.cs
│   │   │   └── ImageProcessor.cs
│   │   │
│   │   └── Resources/
│   │       ├── Styles/
│   │       │   ├── Colors.xaml
│   │       │   └── Styles.xaml
│   │       ├── Fonts/
│   │       │   ├── Orbitron-Bold.ttf
│   │       │   └── Rajdhani-Regular.ttf
│   │       └── Images/
│   │
│   └── GaimerDesktop.Core/               # Shared logic
│       ├── Constants.cs
│       └── Configuration.cs
│
├── tests/
│   └── GaimerDesktop.Tests/
│
└── docs/
    └── ARCHITECTURE.md
```

### 3.3 Dependency Injection Setup

```csharp
// MauiProgram.cs
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()  // For audio visualizer
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Orbitron-Bold.ttf", "OrbitronBold");
                fonts.AddFont("Rajdhani-Regular.ttf", "Rajdhani");
            });

        // Cross-platform services
        builder.Services.AddSingleton<IGeminiService, GeminiLiveService>();
        
        // Platform-specific services
#if WINDOWS
        builder.Services.AddSingleton<IAudioService, Platforms.Windows.AudioService>();
        builder.Services.AddSingleton<IWindowCaptureService, Platforms.Windows.WindowCaptureService>();
#elif MACCATALYST
        builder.Services.AddSingleton<IAudioService, Platforms.MacCatalyst.AudioService>();
        builder.Services.AddSingleton<IWindowCaptureService, Platforms.MacCatalyst.WindowCaptureService>();
#endif

        // ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<AppSelectorViewModel>();

        // Views
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}
```

---

## 4. Window Capture System

### 4.1 Window Capture Service Interface

```csharp
// Services/Interfaces/IWindowCaptureService.cs
public interface IWindowCaptureService : IDisposable
{
    /// <summary>
    /// Get list of all capturable application windows
    /// </summary>
    Task<List<CaptureTarget>> GetCaptureTargetsAsync();
    
    /// <summary>
    /// Start capturing frames from target window at 1 FPS
    /// </summary>
    Task StartCaptureAsync(CaptureTarget target, Action<byte[]> onFrameCaptured);
    
    /// <summary>
    /// Stop capturing
    /// </summary>
    Task StopCaptureAsync();
    
    /// <summary>
    /// Refresh the list of available windows
    /// </summary>
    Task<List<CaptureTarget>> RefreshTargetsAsync();
    
    // State
    bool IsCapturing { get; }
    CaptureTarget? CurrentTarget { get; }
    
    // Events
    event EventHandler<WindowClosedEventArgs> TargetWindowClosed;
    event EventHandler<CaptureErrorEventArgs> ErrorOccurred;
}
```

### 4.2 Capture Target Model

```csharp
// Models/CaptureTarget.cs
public class CaptureTarget
{
    public nint WindowHandle { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[]? Thumbnail { get; set; }  // 64x64 preview
    
    public string DisplayName => string.IsNullOrEmpty(WindowTitle) 
        ? ProcessName 
        : $"{ProcessName} - {WindowTitle}";
}

public class WindowClosedEventArgs : EventArgs
{
    public CaptureTarget ClosedWindow { get; set; } = null!;
    public string Reason { get; set; } = "Unknown";
}

public class CaptureErrorEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}
```

### 4.3 Windows Implementation

```csharp
// Platforms/Windows/WindowCaptureService.cs
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public partial class WindowCaptureService : IWindowCaptureService
{
    private Timer? _captureTimer;
    private Action<byte[]>? _frameCallback;
    private CaptureTarget? _currentTarget;
    private bool _isCapturing;
    
    public bool IsCapturing => _isCapturing;
    public CaptureTarget? CurrentTarget => _currentTarget;
    
    public async Task<List<CaptureTarget>> GetCaptureTargetsAsync()
    {
        return await Task.Run(() =>
        {
            var targets = new List<CaptureTarget>();
            
            EnumWindows((hWnd, lParam) =>
            {
                // Skip invisible windows
                if (!IsWindowVisible(hWnd)) return true;
                
                // Skip minimized windows
                if (IsIconic(hWnd)) return true;
                
                // Get window title
                var titleLength = GetWindowTextLength(hWnd);
                if (titleLength == 0) return true;
                
                var titleBuilder = new StringBuilder(titleLength + 1);
                GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                var title = titleBuilder.ToString();
                
                // Skip empty titles
                if (string.IsNullOrWhiteSpace(title)) return true;
                
                // Get process info
                GetWindowThreadProcessId(hWnd, out uint processId);
                
                try
                {
                    var process = Process.GetProcessById((int)processId);
                    
                    // Skip system processes
                    if (IsSystemProcess(process.ProcessName)) return true;
                    
                    // Get window bounds
                    GetWindowRect(hWnd, out RECT rect);
                    var width = rect.Right - rect.Left;
                    var height = rect.Bottom - rect.Top;
                    
                    // Skip tiny windows
                    if (width < 100 || height < 100) return true;
                    
                    targets.Add(new CaptureTarget
                    {
                        WindowHandle = hWnd,
                        ProcessName = process.ProcessName,
                        WindowTitle = title,
                        ProcessId = (int)processId,
                        Width = width,
                        Height = height,
                        Thumbnail = CaptureThumbnail(hWnd, 64, 64)
                    });
                }
                catch
                {
                    // Process may have exited
                }
                
                return true;
            }, IntPtr.Zero);
            
            return targets.OrderBy(t => t.ProcessName).ToList();
        });
    }
    
    public Task StartCaptureAsync(CaptureTarget target, Action<byte[]> onFrameCaptured)
    {
        _currentTarget = target;
        _frameCallback = onFrameCaptured;
        _isCapturing = true;
        
        // Verify window still exists
        if (!IsWindow(target.WindowHandle))
        {
            throw new InvalidOperationException("Target window no longer exists");
        }
        
        // Capture at 1 FPS
        _captureTimer = new Timer(CaptureFrame, null, 0, 1000);
        
        return Task.CompletedTask;
    }
    
    private void CaptureFrame(object? state)
    {
        if (_currentTarget == null || !_isCapturing) return;
        
        try
        {
            // Check if window still exists
            if (!IsWindow(_currentTarget.WindowHandle))
            {
                TargetWindowClosed?.Invoke(this, new WindowClosedEventArgs
                {
                    ClosedWindow = _currentTarget,
                    Reason = "Window closed"
                });
                StopCaptureAsync().Wait();
                return;
            }
            
            // Check if minimized
            if (IsIconic(_currentTarget.WindowHandle))
            {
                return; // Skip frame but don't stop
            }
            
            // Capture window
            var jpegData = CaptureWindowAsJpeg(_currentTarget.WindowHandle);
            
            if (jpegData != null && jpegData.Length > 0)
            {
                _frameCallback?.Invoke(jpegData);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new CaptureErrorEventArgs
            {
                Message = "Failed to capture frame",
                Exception = ex
            });
        }
    }
    
    private byte[]? CaptureWindowAsJpeg(nint hWnd)
    {
        GetWindowRect(hWnd, out RECT rect);
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        
        if (width <= 0 || height <= 0) return null;
        
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        
        var hdcBitmap = graphics.GetHdc();
        
        // Use PrintWindow for better compatibility
        PrintWindow(hWnd, hdcBitmap, PW_RENDERFULLCONTENT);
        
        graphics.ReleaseHdc(hdcBitmap);
        
        // Scale to 50%
        var scaledWidth = width / 2;
        var scaledHeight = height / 2;
        
        using var scaled = new Bitmap(scaledWidth, scaledHeight);
        using var scaledGraphics = Graphics.FromImage(scaled);
        scaledGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
        scaledGraphics.DrawImage(bitmap, 0, 0, scaledWidth, scaledHeight);
        
        // Convert to JPEG
        using var ms = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 60L);
        
        scaled.Save(ms, encoder, encoderParams);
        return ms.ToArray();
    }
    
    private byte[]? CaptureThumbnail(nint hWnd, int width, int height)
    {
        try
        {
            var fullCapture = CaptureWindowAsJpeg(hWnd);
            if (fullCapture == null) return null;
            
            using var ms = new MemoryStream(fullCapture);
            using var original = Image.FromStream(ms);
            using var thumbnail = original.GetThumbnailImage(width, height, null, IntPtr.Zero);
            
            using var outputMs = new MemoryStream();
            thumbnail.Save(outputMs, ImageFormat.Jpeg);
            return outputMs.ToArray();
        }
        catch
        {
            return null;
        }
    }
    
    public Task StopCaptureAsync()
    {
        _captureTimer?.Dispose();
        _captureTimer = null;
        _isCapturing = false;
        _currentTarget = null;
        return Task.CompletedTask;
    }
    
    public Task<List<CaptureTarget>> RefreshTargetsAsync() => GetCaptureTargetsAsync();
    
    private static bool IsSystemProcess(string processName)
    {
        var systemProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer", "SystemSettings", "TextInputHost", "SearchHost",
            "ShellExperienceHost", "StartMenuExperienceHost", "LockApp"
        };
        return systemProcesses.Contains(processName);
    }
    
    public void Dispose()
    {
        StopCaptureAsync().Wait();
    }
    
    // P/Invoke declarations
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);
    
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    
    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    
    private const uint PW_RENDERFULLCONTENT = 0x00000002;
    
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
    
    public event EventHandler<WindowClosedEventArgs>? TargetWindowClosed;
    public event EventHandler<CaptureErrorEventArgs>? ErrorOccurred;
}
```

### 4.4 macOS Implementation

```csharp
// Platforms/MacCatalyst/WindowCaptureService.cs
using CoreGraphics;
using Foundation;
using ImageIO;

public class WindowCaptureService : IWindowCaptureService
{
    private Timer? _captureTimer;
    private Action<byte[]>? _frameCallback;
    private CaptureTarget? _currentTarget;
    private uint _windowId;
    private bool _isCapturing;
    
    public bool IsCapturing => _isCapturing;
    public CaptureTarget? CurrentTarget => _currentTarget;
    
    public async Task<List<CaptureTarget>> GetCaptureTargetsAsync()
    {
        return await Task.Run(() =>
        {
            var targets = new List<CaptureTarget>();
            
            // Get all on-screen windows
            var windowList = CGWindow.GetWindowInfoList(
                CGWindowListOption.OnScreenOnly | CGWindowListOption.ExcludeDesktopElements,
                CGWindow.NullWindowID
            );
            
            if (windowList == null) return targets;
            
            foreach (var windowInfo in windowList)
            {
                var windowDict = windowInfo as NSDictionary;
                if (windowDict == null) continue;
                
                var windowId = (windowDict["kCGWindowNumber"] as NSNumber)?.UInt32Value;
                var ownerName = windowDict["kCGWindowOwnerName"] as NSString;
                var windowName = windowDict["kCGWindowName"] as NSString;
                var ownerPid = (windowDict["kCGWindowOwnerPID"] as NSNumber)?.Int32Value;
                var boundsDict = windowDict["kCGWindowBounds"] as NSDictionary;
                
                if (windowId == null || ownerName == null) continue;
                
                // Skip Witness itself
                if (ownerPid == NSProcessInfo.ProcessInfo.ProcessIdentifier) continue;
                
                // Parse bounds
                int width = 0, height = 0;
                if (boundsDict != null)
                {
                    width = (boundsDict["Width"] as NSNumber)?.Int32Value ?? 0;
                    height = (boundsDict["Height"] as NSNumber)?.Int32Value ?? 0;
                }
                
                // Skip tiny windows
                if (width < 100 || height < 100) continue;
                
                targets.Add(new CaptureTarget
                {
                    WindowHandle = (nint)windowId.Value,
                    ProcessName = ownerName.ToString(),
                    WindowTitle = windowName?.ToString() ?? ownerName.ToString(),
                    ProcessId = ownerPid ?? 0,
                    Width = width,
                    Height = height,
                    Thumbnail = CaptureThumbnail(windowId.Value)
                });
            }
            
            return targets.OrderBy(t => t.ProcessName).ToList();
        });
    }
    
    public Task StartCaptureAsync(CaptureTarget target, Action<byte[]> onFrameCaptured)
    {
        _currentTarget = target;
        _windowId = (uint)target.WindowHandle;
        _frameCallback = onFrameCaptured;
        _isCapturing = true;
        
        // Capture at 1 FPS
        _captureTimer = new Timer(CaptureFrame, null, 0, 1000);
        
        return Task.CompletedTask;
    }
    
    private void CaptureFrame(object? state)
    {
        if (!_isCapturing) return;
        
        try
        {
            // Capture window by ID
            using var image = CGWindow.CreateImage(
                CGRect.Null,
                CGWindowListOption.IncludingWindow,
                _windowId,
                CGWindowImageOption.BoundsIgnoreFraming
            );
            
            if (image == null)
            {
                TargetWindowClosed?.Invoke(this, new WindowClosedEventArgs
                {
                    ClosedWindow = _currentTarget!,
                    Reason = "Window no longer available"
                });
                StopCaptureAsync().Wait();
                return;
            }
            
            // Scale and convert to JPEG
            var jpegData = ConvertToScaledJpeg(image, 0.5f, 0.6f);
            
            if (jpegData != null)
            {
                _frameCallback?.Invoke(jpegData);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new CaptureErrorEventArgs
            {
                Message = "Failed to capture frame",
                Exception = ex
            });
        }
    }
    
    private byte[]? ConvertToScaledJpeg(CGImage image, float scale, float quality)
    {
        var scaledWidth = (int)(image.Width * scale);
        var scaledHeight = (int)(image.Height * scale);
        
        using var colorSpace = CGColorSpace.CreateDeviceRGB();
        using var context = new CGBitmapContext(
            null,
            scaledWidth,
            scaledHeight,
            8,
            scaledWidth * 4,
            colorSpace,
            CGImageAlphaInfo.PremultipliedFirst
        );
        
        context.InterpolationQuality = CGInterpolationQuality.High;
        context.DrawImage(new CGRect(0, 0, scaledWidth, scaledHeight), image);
        
        using var scaledImage = context.ToImage();
        if (scaledImage == null) return null;
        
        // Convert to JPEG
        using var data = new NSMutableData();
        using var dest = CGImageDestination.Create(data, "public.jpeg", 1);
        
        if (dest == null) return null;
        
        var options = new NSDictionary(
            CGImageDestinationOptionsKeys.LossyCompressionQuality,
            new NSNumber(quality)
        );
        
        dest.AddImage(scaledImage, options);
        dest.Close();
        
        return data.ToArray();
    }
    
    private byte[]? CaptureThumbnail(uint windowId)
    {
        try
        {
            using var image = CGWindow.CreateImage(
                CGRect.Null,
                CGWindowListOption.IncludingWindow,
                windowId,
                CGWindowImageOption.BoundsIgnoreFraming
            );
            
            return image != null ? ConvertToScaledJpeg(image, 0.1f, 0.5f) : null;
        }
        catch
        {
            return null;
        }
    }
    
    public Task StopCaptureAsync()
    {
        _captureTimer?.Dispose();
        _captureTimer = null;
        _isCapturing = false;
        _currentTarget = null;
        return Task.CompletedTask;
    }
    
    public Task<List<CaptureTarget>> RefreshTargetsAsync() => GetCaptureTargetsAsync();
    
    public void Dispose()
    {
        StopCaptureAsync().Wait();
    }
    
    public event EventHandler<WindowClosedEventArgs>? TargetWindowClosed;
    public event EventHandler<CaptureErrorEventArgs>? ErrorOccurred;
}
```

### 4.5 Capture Configuration

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Capture Rate | 1 FPS | Sufficient for AI analysis |
| Scale Factor | 50% | Reduces bandwidth |
| JPEG Quality | 60% | Good compression/quality balance |
| Min Window Size | 100x100 | Filter out tiny windows |
| Thumbnail Size | 64x64 | Preview in selector |

---

## 5. Audio System Design

### 5.1 Audio Service Interface

```csharp
// Services/Interfaces/IAudioService.cs
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
    event EventHandler<VolumeChangedEventArgs> VolumeChanged;
    event EventHandler<AudioErrorEventArgs> ErrorOccurred;
}

public class VolumeChangedEventArgs : EventArgs
{
    public float InputVolume { get; set; }
    public float OutputVolume { get; set; }
}

public class AudioErrorEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}
```

### 5.2 Windows Audio Implementation (NAudio/WASAPI)

```csharp
// Platforms/Windows/AudioService.cs
using NAudio.Wave;
using NAudio.CoreAudioApi;

public class AudioService : IAudioService
{
    private WasapiCapture? _capture;
    private WasapiOut? _player;
    private BufferedWaveProvider? _waveProvider;
    private Action<byte[]>? _audioCallback;
    private HashSet<AudioBufferSourceNode> _activeSources = new();
    private double _nextStartTime;
    
    public int InputSampleRate => 16000;
    public int OutputSampleRate => 24000;
    public bool IsRecording { get; private set; }
    public bool IsPlaying => _player?.PlaybackState == PlaybackState.Playing;
    public float InputVolume { get; private set; }
    public float OutputVolume { get; private set; }
    
    public async Task StartRecordingAsync(Action<byte[]> onAudioCaptured)
    {
        await Task.Run(() =>
        {
            _audioCallback = onAudioCaptured;
            
            // Get default capture device
            var enumerator = new MMDeviceEnumerator();
            var captureDevice = enumerator.GetDefaultAudioEndpoint(
                DataFlow.Capture, Role.Communications);
            
            // Configure for 16kHz mono
            _capture = new WasapiCapture(captureDevice, true, 50); // 50ms buffer
            
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            
            _capture.StartRecording();
            IsRecording = true;
        });
    }
    
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;
        
        // Resample if needed (device may not support 16kHz directly)
        var pcmData = ResampleTo16kMono(e.Buffer, e.BytesRecorded, _capture!.WaveFormat);
        
        // Calculate RMS volume
        InputVolume = CalculateRMS(pcmData);
        VolumeChanged?.Invoke(this, new VolumeChangedEventArgs
        {
            InputVolume = InputVolume,
            OutputVolume = OutputVolume
        });
        
        _audioCallback?.Invoke(pcmData);
    }
    
    private byte[] ResampleTo16kMono(byte[] buffer, int length, WaveFormat sourceFormat)
    {
        if (sourceFormat.SampleRate == 16000 && sourceFormat.Channels == 1)
        {
            var result = new byte[length];
            Buffer.BlockCopy(buffer, 0, result, 0, length);
            return result;
        }
        
        using var inputStream = new RawSourceWaveStream(
            new MemoryStream(buffer, 0, length), sourceFormat);
        
        var targetFormat = new WaveFormat(16000, 16, 1);
        using var resampler = new MediaFoundationResampler(inputStream, targetFormat);
        
        var outputBuffer = new byte[length];
        var bytesRead = resampler.Read(outputBuffer, 0, outputBuffer.Length);
        
        var result2 = new byte[bytesRead];
        Buffer.BlockCopy(outputBuffer, 0, result2, 0, bytesRead);
        return result2;
    }
    
    public Task StopRecordingAsync()
    {
        _capture?.StopRecording();
        IsRecording = false;
        return Task.CompletedTask;
    }
    
    public async Task PlayAudioAsync(byte[] pcmData)
    {
        await Task.Run(() =>
        {
            // Initialize player if needed
            if (_player == null)
            {
                var enumerator = new MMDeviceEnumerator();
                var playbackDevice = enumerator.GetDefaultAudioEndpoint(
                    DataFlow.Render, Role.Multimedia);
                
                _player = new WasapiOut(playbackDevice, AudioClientShareMode.Shared, false, 50);
                
                var playbackFormat = new WaveFormat(OutputSampleRate, 16, 1);
                _waveProvider = new BufferedWaveProvider(playbackFormat)
                {
                    BufferLength = 1024 * 1024, // 1MB buffer
                    DiscardOnBufferOverflow = true
                };
                
                _player.Init(_waveProvider);
            }
            
            // Add samples to buffer
            _waveProvider!.AddSamples(pcmData, 0, pcmData.Length);
            
            // Start playback if not already playing
            if (_player.PlaybackState != PlaybackState.Playing)
            {
                _player.Play();
            }
            
            // Update output volume
            OutputVolume = CalculateRMS(pcmData);
            VolumeChanged?.Invoke(this, new VolumeChangedEventArgs
            {
                InputVolume = InputVolume,
                OutputVolume = OutputVolume
            });
        });
    }
    
    public Task StopPlaybackAsync()
    {
        _player?.Stop();
        _waveProvider?.ClearBuffer();
        OutputVolume = 0;
        return Task.CompletedTask;
    }
    
    public Task InterruptPlaybackAsync()
    {
        _player?.Stop();
        _waveProvider?.ClearBuffer();
        OutputVolume = 0;
        return Task.CompletedTask;
    }
    
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            ErrorOccurred?.Invoke(this, new AudioErrorEventArgs
            {
                Message = "Recording stopped unexpectedly",
                Exception = e.Exception
            });
        }
    }
    
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
    
    public void Dispose()
    {
        _capture?.StopRecording();
        _capture?.Dispose();
        _player?.Stop();
        _player?.Dispose();
    }
    
    public event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
    public event EventHandler<AudioErrorEventArgs>? ErrorOccurred;
}
```

### 5.3 macOS Audio Implementation (AVAudioEngine)

```csharp
// Platforms/MacCatalyst/AudioService.cs
using AVFoundation;
using Foundation;

public class AudioService : IAudioService
{
    private AVAudioEngine? _audioEngine;
    private AVAudioInputNode? _inputNode;
    private AVAudioPlayerNode? _playerNode;
    private AVAudioFormat? _inputFormat;
    private AVAudioFormat? _outputFormat;
    private Action<byte[]>? _audioCallback;
    
    public int InputSampleRate => 16000;
    public int OutputSampleRate => 24000;
    public bool IsRecording { get; private set; }
    public bool IsPlaying => _playerNode?.Playing ?? false;
    public float InputVolume { get; private set; }
    public float OutputVolume { get; private set; }
    
    public async Task StartRecordingAsync(Action<byte[]> onAudioCaptured)
    {
        await Task.Run(() =>
        {
            _audioCallback = onAudioCaptured;
            
            _audioEngine = new AVAudioEngine();
            _inputNode = _audioEngine.InputNode;
            _playerNode = new AVAudioPlayerNode();
            
            _audioEngine.AttachNode(_playerNode);
            
            // Input format: 16kHz mono
            _inputFormat = new AVAudioFormat(
                AVAudioCommonFormat.PCMInt16,
                InputSampleRate,
                1,
                interleaved: true
            );
            
            // Output format: 24kHz mono
            _outputFormat = new AVAudioFormat(
                AVAudioCommonFormat.PCMInt16,
                OutputSampleRate,
                1,
                interleaved: true
            );
            
            // Connect player to output
            _audioEngine.Connect(_playerNode, _audioEngine.OutputNode, _outputFormat);
            
            // Install tap on input
            _inputNode.InstallTapOnBus(0, 4096, _inputFormat, (buffer, when) =>
            {
                var audioData = ExtractPcmData(buffer);
                if (audioData.Length > 0)
                {
                    InputVolume = CalculateRMS(audioData);
                    VolumeChanged?.Invoke(this, new VolumeChangedEventArgs
                    {
                        InputVolume = InputVolume,
                        OutputVolume = OutputVolume
                    });
                    
                    _audioCallback?.Invoke(audioData);
                }
            });
            
            _audioEngine.Prepare();
            _audioEngine.StartAndReturnError(out var error);
            
            if (error != null)
            {
                ErrorOccurred?.Invoke(this, new AudioErrorEventArgs
                {
                    Message = error.LocalizedDescription
                });
                return;
            }
            
            IsRecording = true;
        });
    }
    
    private byte[] ExtractPcmData(AVAudioPcmBuffer buffer)
    {
        var audioBufferList = buffer.AudioBufferList;
        if (audioBufferList.Count == 0) return Array.Empty<byte>();
        
        var audioBuffer = audioBufferList[0];
        var dataLength = (int)audioBuffer.DataByteSize;
        
        if (dataLength == 0) return Array.Empty<byte>();
        
        var data = new byte[dataLength];
        System.Runtime.InteropServices.Marshal.Copy(audioBuffer.Data, data, 0, dataLength);
        return data;
    }
    
    public Task StopRecordingAsync()
    {
        _inputNode?.RemoveTapOnBus(0);
        _audioEngine?.Stop();
        IsRecording = false;
        return Task.CompletedTask;
    }
    
    public async Task PlayAudioAsync(byte[] pcmData)
    {
        await Task.Run(() =>
        {
            if (_playerNode == null || _outputFormat == null) return;
            
            var frameCount = (uint)(pcmData.Length / 2);
            var audioBuffer = new AVAudioPcmBuffer(_outputFormat, frameCount);
            
            System.Runtime.InteropServices.Marshal.Copy(
                pcmData, 0, audioBuffer.AudioBufferList[0].Data, pcmData.Length);
            
            audioBuffer.FrameLength = frameCount;
            
            _playerNode.ScheduleBuffer(audioBuffer, null);
            
            if (!_playerNode.Playing)
            {
                _playerNode.Play();
            }
            
            OutputVolume = CalculateRMS(pcmData);
            VolumeChanged?.Invoke(this, new VolumeChangedEventArgs
            {
                InputVolume = InputVolume,
                OutputVolume = OutputVolume
            });
        });
    }
    
    public Task StopPlaybackAsync()
    {
        _playerNode?.Stop();
        OutputVolume = 0;
        return Task.CompletedTask;
    }
    
    public Task InterruptPlaybackAsync()
    {
        _playerNode?.Stop();
        OutputVolume = 0;
        return Task.CompletedTask;
    }
    
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
    
    public void Dispose()
    {
        _inputNode?.RemoveTapOnBus(0);
        _audioEngine?.Stop();
        _audioEngine?.Dispose();
        _playerNode?.Dispose();
    }
    
    public event EventHandler<VolumeChangedEventArgs>? VolumeChanged;
    public event EventHandler<AudioErrorEventArgs>? ErrorOccurred;
}
```

### 5.4 Audio Configuration

| Parameter | Input | Output |
|-----------|-------|--------|
| Sample Rate | 16,000 Hz | 24,000 Hz |
| Bit Depth | 16-bit | 16-bit |
| Channels | 1 (Mono) | 1 (Mono) |
| Buffer Size | 4096 samples | Variable |
| Format | PCM | PCM |

---

## 6. Network Layer

### 6.1 Gemini Service Interface

```csharp
// Services/Interfaces/IGeminiService.cs
public interface IGeminiService : IDisposable
{
    // Connection
    Task ConnectAsync();
    Task DisconnectAsync();
    
    // Send data
    Task SendAudioAsync(byte[] pcmData);
    Task SendImageAsync(byte[] jpegData);
    
    // State
    bool IsConnected { get; }
    ConnectionState State { get; }
    
    // Events
    event EventHandler<byte[]> AudioReceived;
    event EventHandler<bool> InterruptionReceived;
    event EventHandler<ConnectionState> StateChanged;
    event EventHandler<string> ErrorOccurred;
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}
```

### 6.2 Gemini Live Service Implementation

```csharp
// Services/GeminiLiveService.cs
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

public class GeminiLiveService : IGeminiService
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private readonly string _apiKey;
    private readonly string _model = "gemini-2.5-flash-preview-native-audio-dialog";
    
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    
    public GeminiLiveService(IConfiguration configuration)
    {
        _apiKey = configuration["GeminiApiKey"] 
            ?? throw new InvalidOperationException("GeminiApiKey not configured");
    }
    
    public async Task ConnectAsync()
    {
        try
        {
            State = ConnectionState.Connecting;
            StateChanged?.Invoke(this, State);
            
            _webSocket = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            
            var uri = new Uri(
                $"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={_apiKey}");
            
            await _webSocket.ConnectAsync(uri, _cts.Token);
            
            // Send setup message
            await SendSetupMessageAsync();
            
            State = ConnectionState.Connected;
            StateChanged?.Invoke(this, State);
            
            // Start receiving
            _ = ReceiveLoopAsync();
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            StateChanged?.Invoke(this, State);
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }
    
    private async Task SendSetupMessageAsync()
    {
        var setup = new
        {
            setup = new
            {
                model = $"models/{_model}",
                generation_config = new
                {
                    response_modalities = new[] { "AUDIO" },
                    speech_config = new
                    {
                        voice_config = new
                        {
                            prebuilt_voice_config = new
                            {
                                voice_name = "Fenrir"
                            }
                        }
                    }
                },
                system_instruction = new
                {
                    parts = new[]
                    {
                        new { text = SystemPrompt.Content }
                    }
                }
            }
        };
        
        var json = JsonSerializer.Serialize(setup);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await _webSocket!.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            _cts!.Token
        );
    }
    
    public async Task SendAudioAsync(byte[] pcmData)
    {
        if (!IsConnected) return;
        
        var base64 = Convert.ToBase64String(pcmData);
        
        var message = new
        {
            realtime_input = new
            {
                media_chunks = new[]
                {
                    new
                    {
                        mime_type = "audio/pcm;rate=16000",
                        data = base64
                    }
                }
            }
        };
        
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await _webSocket!.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            _cts!.Token
        );
    }
    
    public async Task SendImageAsync(byte[] jpegData)
    {
        if (!IsConnected) return;
        
        var base64 = Convert.ToBase64String(jpegData);
        
        var message = new
        {
            realtime_input = new
            {
                media_chunks = new[]
                {
                    new
                    {
                        mime_type = "image/jpeg",
                        data = base64
                    }
                }
            }
        };
        
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await _webSocket!.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            _cts!.Token
        );
    }
    
    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[1024 * 64]; // 64KB buffer
        
        try
        {
            while (_webSocket?.State == WebSocketState.Open && !_cts!.Token.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    _cts.Token
                );
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await DisconnectAsync();
                    break;
                }
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessMessage(json);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            State = ConnectionState.Error;
            StateChanged?.Invoke(this, State);
        }
    }
    
    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // Check for audio response
            if (root.TryGetProperty("serverContent", out var serverContent))
            {
                // Check for interruption
                if (serverContent.TryGetProperty("interrupted", out var interrupted) &&
                    interrupted.GetBoolean())
                {
                    InterruptionReceived?.Invoke(this, true);
                    return;
                }
                
                // Extract audio data
                if (serverContent.TryGetProperty("modelTurn", out var modelTurn) &&
                    modelTurn.TryGetProperty("parts", out var parts))
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("inlineData", out var inlineData) &&
                            inlineData.TryGetProperty("data", out var data))
                        {
                            var base64Audio = data.GetString();
                            if (!string.IsNullOrEmpty(base64Audio))
                            {
                                var pcmData = Convert.FromBase64String(base64Audio);
                                AudioReceived?.Invoke(this, pcmData);
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Invalid JSON, ignore
        }
    }
    
    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        
        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "User disconnected",
                CancellationToken.None
            );
        }
        
        _webSocket?.Dispose();
        _webSocket = null;
        
        State = ConnectionState.Disconnected;
        StateChanged?.Invoke(this, State);
    }
    
    public void Dispose()
    {
        DisconnectAsync().Wait();
        _cts?.Dispose();
    }
    
    public event EventHandler<byte[]>? AudioReceived;
    public event EventHandler<bool>? InterruptionReceived;
    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<string>? ErrorOccurred;
}
```

### 6.3 System Prompt

```csharp
// Constants/SystemPrompt.cs
public static class SystemPrompt
{
    public const string Content = @"
You are Witness, a highly advanced visual conversational AI designed for desktop PCs.
Your personality is that of a chill, enthusiastic, and knowledgeable gamer.
You use mild gamer slang (e.g., 'GG', 'Level up', 'Nerf', 'Buff', 'Lag') naturally, but don't overdo it.
You are helpful, witty, and cool.

BEHAVIOR:
1. When you cannot see any visual input: Be conversational, ask what they're playing or working on, maintain a 'lobby' or 'voice chat' vibe.

2. When you receive window screenshots: Act like a co-op partner. Analyze what you see.
   - If it's a game: Comment on gameplay, UI, graphics, strategy
   - If it's code: Offer 'backseat coding' style helpful advice
   - If it's a browser: Comment on what they're browsing, offer insights

3. Your responses should be relatively concise and spoken (voice interaction).

4. You cannot hear the application's audio, only see screenshots. Ask the user to describe sounds if needed.

Always assume the user is a friend you're hanging out with on Discord or TeamSpeak.
";
}
```

---

## 7. UI/UX Implementation

### 7.1 Color Palette (XAML)

```xml
<!-- Resources/Styles/Colors.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    
    <!-- Background Colors -->
    <Color x:Key="BgPrimary">#050505</Color>
    <Color x:Key="BgSecondary">#0f172a</Color>
    <Color x:Key="BgCard">#1e293b</Color>
    
    <!-- Accent Colors -->
    <Color x:Key="AccentCyan">#06b6d4</Color>
    <Color x:Key="AccentPurple">#a855f7</Color>
    <Color x:Key="AccentBlue">#3b82f6</Color>
    <Color x:Key="AccentGreen">#22c55e</Color>
    <Color x:Key="AccentRed">#ef4444</Color>
    
    <!-- Text Colors -->
    <Color x:Key="TextPrimary">#e0e0e0</Color>
    <Color x:Key="TextSecondary">#64748b</Color>
    <Color x:Key="TextMuted">#475569</Color>
    
    <!-- Gradients -->
    <LinearGradientBrush x:Key="GradientAccent" StartPoint="0,0" EndPoint="1,1">
        <GradientStop Color="#a855f7" Offset="0"/>
        <GradientStop Color="#06b6d4" Offset="1"/>
    </LinearGradientBrush>
    
</ResourceDictionary>
```

### 7.2 Main Page Layout

```xml
<!-- Views/MainPage.xaml -->
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:GaimerDesktop.ViewModels"
             xmlns:controls="clr-namespace:GaimerDesktop.Controls"
             x:Class="GaimerDesktop.Views.MainPage"
             BackgroundColor="{StaticResource BgPrimary}">
    
    <Grid RowDefinitions="Auto,*,Auto" Padding="24">
        
        <!-- Header -->
        <HorizontalStackLayout Grid.Row="0" Spacing="12">
            <Image Source="witness_logo.png" HeightRequest="40"/>
            <Label Text="WITNESS" 
                   FontFamily="OrbitronBold" 
                   FontSize="28"
                   TextColor="{StaticResource AccentCyan}"
                   VerticalOptions="Center"/>
            
            <!-- Connection indicator -->
            <Border BackgroundColor="{Binding ConnectionColor}"
                    StrokeShape="RoundRectangle 8"
                    Padding="12,6"
                    Margin="20,0,0,0">
                <Label Text="{Binding ConnectionStatus}"
                       FontFamily="Rajdhani"
                       TextColor="{StaticResource TextPrimary}"/>
            </Border>
        </HorizontalStackLayout>
        
        <!-- Main Content -->
        <Grid Grid.Row="1" ColumnDefinitions="*,320" Margin="0,24">
            
            <!-- Left: Target Preview + Visualizer -->
            <Grid RowDefinitions="*,200">
                
                <!-- Window Preview -->
                <Border BackgroundColor="{StaticResource BgSecondary}"
                        StrokeShape="RoundRectangle 12"
                        Stroke="{StaticResource AccentPurple}"
                        StrokeThickness="1">
                    <Grid>
                        <!-- No Target Selected State -->
                        <VerticalStackLayout IsVisible="{Binding NoTargetSelected}"
                                            VerticalOptions="Center"
                                            HorizontalOptions="Center"
                                            Spacing="16">
                            <Label Text="📺" FontSize="64"/>
                            <Label Text="No Application Selected"
                                   FontFamily="Rajdhani"
                                   FontSize="20"
                                   TextColor="{StaticResource TextSecondary}"/>
                            <Button Text="Select Application"
                                    Command="{Binding SelectTargetCommand}"
                                    BackgroundColor="{StaticResource AccentPurple}"/>
                        </VerticalStackLayout>
                        
                        <!-- Target Preview -->
                        <Grid IsVisible="{Binding HasTarget}">
                            <Image Source="{Binding CurrentFrameSource}"
                                   Aspect="AspectFit"/>
                            
                            <!-- HUD Overlay -->
                            <Border BackgroundColor="#80000000"
                                    Padding="8,4"
                                    VerticalOptions="Start"
                                    HorizontalOptions="Start"
                                    Margin="12">
                                <Label Text="{Binding TargetDisplayName}"
                                       FontFamily="Rajdhani"
                                       TextColor="{StaticResource AccentCyan}"/>
                            </Border>
                        </Grid>
                    </Grid>
                </Border>
                
                <!-- Audio Visualizer -->
                <controls:AudioVisualizer Grid.Row="1"
                                         InputVolume="{Binding InputVolume}"
                                         OutputVolume="{Binding OutputVolume}"
                                         Margin="0,16,0,0"/>
            </Grid>
            
            <!-- Right: App Selector Panel -->
            <Border Grid.Column="1"
                    BackgroundColor="{StaticResource BgSecondary}"
                    StrokeShape="RoundRectangle 12"
                    Margin="16,0,0,0"
                    Padding="16">
                <Grid RowDefinitions="Auto,*,Auto">
                    
                    <!-- Header -->
                    <HorizontalStackLayout Spacing="8">
                        <Label Text="🎯" FontSize="20"/>
                        <Label Text="TARGET APPLICATION"
                               FontFamily="OrbitronBold"
                               FontSize="14"
                               TextColor="{StaticResource TextSecondary}"/>
                        <Button Text="↻" 
                                Command="{Binding RefreshTargetsCommand}"
                                BackgroundColor="Transparent"
                                TextColor="{StaticResource AccentCyan}"
                                WidthRequest="32"/>
                    </HorizontalStackLayout>
                    
                    <!-- App List -->
                    <CollectionView Grid.Row="1"
                                   ItemsSource="{Binding CaptureTargets}"
                                   SelectionMode="Single"
                                   SelectedItem="{Binding SelectedTarget}"
                                   Margin="0,12">
                        <CollectionView.ItemTemplate>
                            <DataTemplate>
                                <Border BackgroundColor="{StaticResource BgCard}"
                                        StrokeShape="RoundRectangle 8"
                                        Padding="8"
                                        Margin="0,4">
                                    <HorizontalStackLayout Spacing="12">
                                        <Image Source="{Binding ThumbnailSource}"
                                               WidthRequest="48"
                                               HeightRequest="48"/>
                                        <VerticalStackLayout VerticalOptions="Center">
                                            <Label Text="{Binding ProcessName}"
                                                   FontFamily="Rajdhani"
                                                   FontSize="14"
                                                   FontAttributes="Bold"
                                                   TextColor="{StaticResource TextPrimary}"/>
                                            <Label Text="{Binding WindowTitle}"
                                                   FontFamily="Rajdhani"
                                                   FontSize="12"
                                                   TextColor="{StaticResource TextSecondary}"
                                                   MaxLines="1"/>
                                        </VerticalStackLayout>
                                    </HorizontalStackLayout>
                                </Border>
                            </DataTemplate>
                        </CollectionView.ItemTemplate>
                    </CollectionView>
                    
                    <!-- Connect Button -->
                    <Button Grid.Row="2"
                            Text="{Binding ConnectButtonText}"
                            Command="{Binding ToggleConnectionCommand}"
                            BackgroundColor="{Binding ConnectButtonColor}"
                            TextColor="White"
                            FontFamily="OrbitronBold"
                            HeightRequest="50"
                            CornerRadius="8"/>
                </Grid>
            </Border>
        </Grid>
        
        <!-- Footer Status -->
        <HorizontalStackLayout Grid.Row="2" 
                              Spacing="24"
                              HorizontalOptions="Center">
            <Label Text="{Binding StatusMessage}"
                   FontFamily="Rajdhani"
                   TextColor="{StaticResource TextMuted}"/>
        </HorizontalStackLayout>
        
    </Grid>
</ContentPage>
```

### 7.3 Main ViewModel

```csharp
// ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ObservableObject
{
    private readonly IAudioService _audioService;
    private readonly IWindowCaptureService _captureService;
    private readonly IGeminiService _geminiService;
    
    [ObservableProperty] private ConnectionState _connectionState = ConnectionState.Disconnected;
    [ObservableProperty] private float _inputVolume;
    [ObservableProperty] private float _outputVolume;
    [ObservableProperty] private List<CaptureTarget> _captureTargets = new();
    [ObservableProperty] private CaptureTarget? _selectedTarget;
    [ObservableProperty] private ImageSource? _currentFrameSource;
    [ObservableProperty] private string _statusMessage = "Ready";
    
    public bool NoTargetSelected => SelectedTarget == null;
    public bool HasTarget => SelectedTarget != null;
    public string TargetDisplayName => SelectedTarget?.DisplayName ?? "";
    
    public string ConnectionStatus => ConnectionState switch
    {
        ConnectionState.Connected => "ONLINE",
        ConnectionState.Connecting => "CONNECTING...",
        ConnectionState.Reconnecting => "RECONNECTING...",
        _ => "OFFLINE"
    };
    
    public Color ConnectionColor => ConnectionState switch
    {
        ConnectionState.Connected => Colors.Green,
        ConnectionState.Connecting => Colors.Yellow,
        _ => Colors.Gray
    };
    
    public string ConnectButtonText => ConnectionState == ConnectionState.Connected 
        ? "DISCONNECT" : "CONNECT";
    
    public Color ConnectButtonColor => ConnectionState == ConnectionState.Connected
        ? Color.FromArgb("#ef4444") : Color.FromArgb("#a855f7");
    
    public MainViewModel(
        IAudioService audioService,
        IWindowCaptureService captureService,
        IGeminiService geminiService)
    {
        _audioService = audioService;
        _captureService = captureService;
        _geminiService = geminiService;
        
        // Subscribe to events
        _audioService.VolumeChanged += (s, e) =>
        {
            InputVolume = e.InputVolume;
            OutputVolume = e.OutputVolume;
        };
        
        _geminiService.StateChanged += (s, state) =>
        {
            ConnectionState = state;
            OnPropertyChanged(nameof(ConnectionStatus));
            OnPropertyChanged(nameof(ConnectionColor));
            OnPropertyChanged(nameof(ConnectButtonText));
            OnPropertyChanged(nameof(ConnectButtonColor));
        };
        
        _geminiService.AudioReceived += async (s, pcm) =>
        {
            await _audioService.PlayAudioAsync(pcm);
        };
        
        _geminiService.InterruptionReceived += async (s, _) =>
        {
            await _audioService.InterruptPlaybackAsync();
        };
        
        _captureService.TargetWindowClosed += (s, e) =>
        {
            SelectedTarget = null;
            StatusMessage = $"Target window closed: {e.Reason}";
        };
        
        // Load initial targets
        _ = RefreshTargetsAsync();
    }
    
    [RelayCommand]
    private async Task RefreshTargetsAsync()
    {
        CaptureTargets = await _captureService.GetCaptureTargetsAsync();
    }
    
    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (ConnectionState == ConnectionState.Connected)
        {
            await DisconnectAsync();
        }
        else
        {
            await ConnectAsync();
        }
    }
    
    private async Task ConnectAsync()
    {
        if (SelectedTarget == null)
        {
            StatusMessage = "Please select a target application first";
            return;
        }
        
        try
        {
            StatusMessage = "Connecting to Gemini...";
            
            // Connect to API
            await _geminiService.ConnectAsync();
            
            // Start microphone
            await _audioService.StartRecordingAsync(async pcm =>
            {
                await _geminiService.SendAudioAsync(pcm);
            });
            
            // Start window capture
            await _captureService.StartCaptureAsync(SelectedTarget, async jpeg =>
            {
                // Update preview
                CurrentFrameSource = ImageSource.FromStream(() => new MemoryStream(jpeg));
                
                // Send to API
                await _geminiService.SendImageAsync(jpeg);
            });
            
            StatusMessage = $"Connected - Watching {SelectedTarget.ProcessName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            await DisconnectAsync();
        }
    }
    
    private async Task DisconnectAsync()
    {
        await _captureService.StopCaptureAsync();
        await _audioService.StopRecordingAsync();
        await _audioService.StopPlaybackAsync();
        await _geminiService.DisconnectAsync();
        
        CurrentFrameSource = null;
        StatusMessage = "Disconnected";
    }
    
    partial void OnSelectedTargetChanged(CaptureTarget? value)
    {
        OnPropertyChanged(nameof(NoTargetSelected));
        OnPropertyChanged(nameof(HasTarget));
        OnPropertyChanged(nameof(TargetDisplayName));
    }
}
```

---

## 8. Platform Services

### 8.1 Windows Requirements

| Requirement | Version | Purpose |
|-------------|---------|---------|
| Windows | 10 1903+ | Graphics Capture API |
| .NET | 8.0 | Runtime |
| WASAPI | Built-in | Audio capture/playback |

**Capabilities Required (Package.appxmanifest):**
```xml
<Capabilities>
    <Capability Name="internetClient" />
    <DeviceCapability Name="microphone" />
    <rescap:Capability Name="graphicsCapture" />
</Capabilities>
```

### 8.2 macOS Requirements

| Requirement | Version | Purpose |
|-------------|---------|---------|
| macOS | 12.0+ | CGWindowList, AVAudioEngine |
| Xcode | 14+ | Build tools |

**Entitlements Required (Entitlements.plist):**
```xml
<key>com.apple.security.device.audio-input</key>
<true/>
<key>com.apple.security.personal-information.photos-library</key>
<true/>
```

**Info.plist:**
```xml
<key>NSMicrophoneUsageDescription</key>
<string>Witness needs microphone access for voice chat with the AI.</string>
<key>NSScreenCaptureUsageDescription</key>
<string>Witness needs screen capture permission to analyze application windows.</string>
```

---

## 9. Error Handling & Recovery

### 9.1 Error Categories

| Category | Examples | Recovery |
|----------|----------|----------|
| **Connection** | WebSocket failure, timeout | Auto-reconnect with backoff |
| **Audio** | Device unplugged, permission denied | Re-initialize, notify user |
| **Capture** | Window closed, minimized | Stop capture, notify user |
| **API** | Rate limit, invalid response | Backoff, retry |

### 9.2 Reconnection Strategy

```csharp
// Services/GeminiLiveService.cs (addition)
private async Task ReconnectWithBackoffAsync()
{
    var delays = new[] { 1000, 2000, 5000, 10000, 30000 };
    
    for (int attempt = 0; attempt < delays.Length; attempt++)
    {
        State = ConnectionState.Reconnecting;
        StateChanged?.Invoke(this, State);
        
        await Task.Delay(delays[attempt]);
        
        try
        {
            await ConnectAsync();
            if (IsConnected) return;
        }
        catch
        {
            // Continue to next attempt
        }
    }
    
    State = ConnectionState.Error;
    StateChanged?.Invoke(this, State);
    ErrorOccurred?.Invoke(this, "Failed to reconnect after multiple attempts");
}
```

### 9.3 Resource Cleanup

```csharp
// Cleanup checklist on disconnect:
// 1. Stop audio recording
// 2. Stop audio playback  
// 3. Stop window capture
// 4. Close WebSocket
// 5. Clear buffers
// 6. Reset state
```

---

## 10. Build & Distribution

### 10.1 Build Commands

**Windows:**
```bash
dotnet publish -c Release -f net8.0-windows10.0.19041.0 -r win-x64 --self-contained
```

**macOS (Intel):**
```bash
dotnet publish -c Release -f net8.0-maccatalyst -r osx-x64 --self-contained
```

**macOS (Apple Silicon):**
```bash
dotnet publish -c Release -f net8.0-maccatalyst -r osx-arm64 --self-contained
```

### 10.2 Distribution

| Platform | Format | Signing |
|----------|--------|---------|
| Windows | MSIX | Authenticode certificate |
| macOS | DMG/PKG | Apple Developer ID |

### 10.3 NuGet Packages

```xml
<ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="SkiaSharp.Views.Maui.Controls" Version="2.88.7" />
</ItemGroup>

<!-- Windows only -->
<ItemGroup Condition="$(TargetFramework.Contains('windows'))">
    <PackageReference Include="NAudio" Version="2.2.1" />
</ItemGroup>
```

---

## 11. Implementation Roadmap

### Phase 1: Foundation (Week 1-2)
- [ ] Project setup with .NET MAUI
- [ ] Basic UI layout
- [ ] Window enumeration (both platforms)
- [ ] Window capture (basic)

### Phase 2: Audio (Week 2-3)
- [ ] Microphone capture (Windows)
- [ ] Microphone capture (macOS)
- [ ] Audio playback (Windows)
- [ ] Audio playback (macOS)

### Phase 3: Integration (Week 3-4)
- [ ] WebSocket client
- [ ] Gemini API integration
- [ ] Audio/image sending
- [ ] Response handling

### Phase 4: Polish (Week 4-5)
- [ ] UI styling (gamer theme)
- [ ] Audio visualizer
- [ ] Error handling
- [ ] Testing

### Phase 5: Distribution (Week 5-6)
- [ ] Windows packaging (MSIX)
- [ ] macOS packaging (DMG)
- [ ] Code signing
- [ ] Documentation

---

## Appendix A: Future Enhancements (v2.0)

| Feature | Complexity | Notes |
|---------|------------|-------|
| App audio capture | High | Windows: WASAPI sessions, macOS: ScreenCaptureKit |
| Multiple windows | Medium | Track multiple targets |
| Hotkey support | Low | Global keyboard hooks |
| Custom voices | Low | Gemini API supports multiple voices |
| Session history | Medium | Store conversation logs |

---

**Document Version History**

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | Dec 9, 2024 | Initial specification (incomplete) |
| 2.0.0 | Dec 9, 2024 | Complete rewrite: Image-only capture, removed camera, removed app audio, completed all sections |

---

*This document serves as the implementation guide for Witness Desktop v1.0 using .NET MAUI with a focus on window image capture and voice interaction.*
