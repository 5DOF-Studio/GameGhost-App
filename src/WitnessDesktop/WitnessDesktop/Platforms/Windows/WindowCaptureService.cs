using System.Diagnostics;
using WitnessDesktop.Models;
using WitnessDesktop.Services;
using static WitnessDesktop.Platforms.Windows.NativeMethods;

namespace WitnessDesktop.Platforms.Windows;

/// <summary>
/// Windows screen capture service using PrintWindow + GDI.
/// Enumerates visible top-level windows and captures frames at ~1 FPS.
/// </summary>
public sealed class WindowCaptureService : IWindowCaptureService, IDisposable
{
    private const int MinWindowDimension = 200;
    private const int CaptureIntervalMs = 1000; // 1 FPS

    private readonly object _gate = new();
    private Timer? _captureTimer;
    private bool _disposed;

    public event EventHandler<byte[]>? FrameCaptured;
    public bool IsCapturing { get; private set; }
    public CaptureTarget? CurrentTarget { get; private set; }

    public Task<IReadOnlyList<CaptureTarget>> GetCaptureTargetsAsync()
    {
        var targets = new List<CaptureTarget>();
        var selfPid = (uint)Process.GetCurrentProcess().Id;

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            // Skip cloaked windows (UWP suspended, other virtual desktops)
            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;

            // Get window title
            var titleBuf = new char[512];
            var titleLen = GetWindowText(hWnd, titleBuf, titleBuf.Length);
            if (titleLen == 0) return true; // Skip untitled windows

            var windowTitle = new string(titleBuf, 0, titleLen);

            // Skip our own process
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == selfPid) return true;

            // Get window bounds (prefer DWM extended frame bounds for accuracy)
            RECT rect;
            if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, System.Runtime.InteropServices.Marshal.SizeOf<RECT>()) != 0)
            {
                if (!GetWindowRect(hWnd, out rect)) return true;
            }

            if (rect.Width < MinWindowDimension || rect.Height < MinWindowDimension)
                return true;

            // Get process name
            string processName;
            try
            {
                var proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName;
            }
            catch
            {
                processName = "Unknown";
            }

            var target = new CaptureTarget
            {
                Handle = hWnd,
                ProcessName = processName,
                WindowTitle = windowTitle
            };

            // Detect chess sites for badge
            if (windowTitle.Contains("chess", StringComparison.OrdinalIgnoreCase) ||
                windowTitle.Contains("lichess", StringComparison.OrdinalIgnoreCase))
            {
                target.ChessBadge = windowTitle.Contains("lichess", StringComparison.OrdinalIgnoreCase)
                    ? "♟️ Lichess"
                    : "♟️ Chess.com";
            }

            // Generate thumbnail
            var thumbnail = CaptureWindowThumbnail(hWnd);
            if (thumbnail.Length > 0)
                target.Thumbnail = thumbnail;

            targets.Add(target);
            return true;
        }, IntPtr.Zero);

        return Task.FromResult<IReadOnlyList<CaptureTarget>>(targets);
    }

    public Task StartCaptureAsync(CaptureTarget target)
    {
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            if (IsCapturing) return Task.CompletedTask;

            CurrentTarget = target;
            IsCapturing = true;

            _captureTimer = new Timer(OnCaptureTimerTick, null, 0, CaptureIntervalMs);
        }

        return Task.CompletedTask;
    }

    public Task StopCaptureAsync()
    {
        lock (_gate)
        {
            IsCapturing = false;
            CurrentTarget = null;
            _captureTimer?.Dispose();
            _captureTimer = null;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            IsCapturing = false;
            CurrentTarget = null;
            _captureTimer?.Dispose();
            _captureTimer = null;
        }
    }

    private void OnCaptureTimerTick(object? state)
    {
        CaptureTarget? target;
        lock (_gate)
        {
            if (!IsCapturing || _disposed) return;
            target = CurrentTarget;
        }

        if (target is null) return;

        try
        {
            var frameData = CaptureWindowAsPng(target.Handle);
            if (frameData.Length > 0)
            {
                var compressed = ImageProcessor.ScaleAndCompress(frameData);
                if (compressed.Length > 0)
                {
                    FrameCaptured?.Invoke(this, compressed);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Capture] Frame capture failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Captures a window using PrintWindow + GDI, returns PNG-encoded bytes via SkiaSharp.
    /// </summary>
    private static byte[] CaptureWindowAsPng(IntPtr hWnd)
    {
        // Get window dimensions
        RECT rect;
        if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, System.Runtime.InteropServices.Marshal.SizeOf<RECT>()) != 0)
        {
            if (!GetWindowRect(hWnd, out rect)) return [];
        }

        int width = rect.Width;
        int height = rect.Height;
        if (width <= 0 || height <= 0) return [];

        IntPtr hdcScreen = IntPtr.Zero;
        IntPtr hdcMem = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOldBitmap = IntPtr.Zero;

        try
        {
            // Create a memory DC compatible with the screen
            hdcScreen = CreateCompatibleDC(IntPtr.Zero);
            if (hdcScreen == IntPtr.Zero) return [];

            hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
            if (hBitmap == IntPtr.Zero) return [];

            hdcMem = CreateCompatibleDC(hdcScreen);
            if (hdcMem == IntPtr.Zero) return [];

            hOldBitmap = SelectObject(hdcMem, hBitmap);

            // PrintWindow with PW_RENDERFULLCONTENT for DWM-composed content
            if (!PrintWindow(hWnd, hdcMem, PW_RENDERFULLCONTENT))
                return [];

            // Read pixel data via GetDIBits
            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // Top-down DIB
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB
                }
            };

            var pixelData = new byte[width * height * 4];
            int result = GetDIBits(hdcMem, hBitmap, 0, (uint)height, pixelData, ref bmi, DIB_RGB_COLORS);
            if (result == 0) return [];

            // Convert BGRA → RGBA (SkiaSharp expects RGBA with SKColorType.Rgba8888,
            // but SKColorType.Bgra8888 matches Windows GDI output directly)
            using var skBitmap = new SkiaSharp.SKBitmap(width, height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, skBitmap.GetPixels(), pixelData.Length);

            using var image = SkiaSharp.SKImage.FromBitmap(skBitmap);
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        finally
        {
            if (hOldBitmap != IntPtr.Zero && hdcMem != IntPtr.Zero)
                SelectObject(hdcMem, hOldBitmap);
            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);
            if (hdcMem != IntPtr.Zero)
                DeleteDC(hdcMem);
            if (hdcScreen != IntPtr.Zero)
                DeleteDC(hdcScreen);
        }
    }

    private static byte[] CaptureWindowThumbnail(IntPtr hWnd)
    {
        var pngData = CaptureWindowAsPng(hWnd);
        if (pngData.Length == 0) return [];
        return ImageProcessor.GenerateThumbnail(pngData);
    }
}
