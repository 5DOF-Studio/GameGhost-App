using Foundation;
using ObjCRuntime;
using SkiaSharp;
using UIKit;
using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// macOS screen capture service.
/// - Window enumeration: CGWindowListCopyWindowInfo (still works for metadata)
/// - Frame capture (macOS 14+ / MacCatalyst 18.2+): ScreenCaptureKit via native Swift helper
/// - Frame capture (fallback): CGDisplayCreateImage (full display) + SkiaSharp crop to window bounds
///
/// SCK captures GPU/Metal-rendered content that CGDisplayCreateImage misses.
/// The native helper (GaimerScreenCapture.xcframework) wraps SCScreenshotManager.
/// </summary>
public sealed class WindowCaptureService : IWindowCaptureService, IDisposable
{
    private const string kCGWindowNumber = "kCGWindowNumber";
    private const string kCGWindowOwnerName = "kCGWindowOwnerName";
    private const string kCGWindowName = "kCGWindowName";
    private const string kCGWindowLayer = "kCGWindowLayer";
    private const string kCGWindowBounds = "kCGWindowBounds";
    private const string kCGWindowOwnerPID = "kCGWindowOwnerPID";

    /// <summary>Pre-allocated NSString keys to avoid creating new NSString objects on every call.</summary>
    private static readonly NSString _nsKeyWindowNumber = new(kCGWindowNumber);
    private static readonly NSString _nsKeyOwnerName = new(kCGWindowOwnerName);
    private static readonly NSString _nsKeyWindowName = new(kCGWindowName);
    private static readonly NSString _nsKeyWindowLayer = new(kCGWindowLayer);
    private static readonly NSString _nsKeyWindowBounds = new(kCGWindowBounds);
    private static readonly NSString _nsKeyOwnerPID = new(kCGWindowOwnerPID);
    private static readonly NSString _nsKeyX = new("X");
    private static readonly NSString _nsKeyY = new("Y");
    private static readonly NSString _nsKeyWidth = new("Width");
    private static readonly NSString _nsKeyHeight = new("Height");

    private const int MinWindowDimension = 200;
    private const int CaptureIntervalMs = 30000; // Default: 1 frame per 30 seconds
    private int _captureCount;

    /// <summary>Cached at class load -- SCK availability does not change at runtime.</summary>
    private static readonly bool _sckAvailable = CheckSckAvailability();

    private readonly object _gate = new();
    private Timer? _captureTimer;
    private bool _disposed;

    public event EventHandler<byte[]>? FrameCaptured;
    public bool IsCapturing { get; private set; }
    public CaptureTarget? CurrentTarget { get; private set; }

    public Task<IReadOnlyList<CaptureTarget>> GetCaptureTargetsAsync()
    {
        // Request Screen Recording permission once (triggers a single system dialog)
        EnsureScreenRecordingPermission();

        var targets = new List<CaptureTarget>();

        var windowList = NativeMethods.CGWindowListCopyWindowInfo(
            CGWindowListOption.OptionOnScreenOnly | CGWindowListOption.ExcludeDesktopElements,
            0);

        if (windowList == IntPtr.Zero)
            return Task.FromResult<IReadOnlyList<CaptureTarget>>(targets);

        try
        {
            using var array = Runtime.GetNSObject<NSArray>(windowList);
            if (array is null) return Task.FromResult<IReadOnlyList<CaptureTarget>>(targets);

            var selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;

            for (nuint i = 0; i < array.Count; i++)
            {
                var dictHandle = array.ValueAt(i);
                using var dict = Runtime.GetNSObject<NSDictionary>(dictHandle);
                if (dict is null) continue;

                var layer = GetIntValue(dict, _nsKeyWindowLayer);
                if (layer != 0) continue;

                var ownerPid = GetIntValue(dict, _nsKeyOwnerPID);
                if (ownerPid == selfPid) continue;

                var ownerName = GetStringValue(dict, _nsKeyOwnerName) ?? "Unknown";
                var windowName = GetStringValue(dict, _nsKeyWindowName) ?? "";
                var windowId = (uint)GetIntValue(dict, _nsKeyWindowNumber);

                var bounds = GetBoundsRect(dict, _nsKeyWindowBounds);
                if (bounds.Width < MinWindowDimension || bounds.Height < MinWindowDimension)
                    continue;

                var displayTitle = string.IsNullOrEmpty(windowName) ? ownerName : windowName;

                var target = new CaptureTarget
                {
                    Handle = (nint)windowId,
                    ProcessName = ownerName,
                    WindowTitle = displayTitle,
                    BoundsX = bounds.X,
                    BoundsY = bounds.Y,
                    BoundsWidth = bounds.Width,
                    BoundsHeight = bounds.Height
                };

                if (displayTitle.Contains("chess", StringComparison.OrdinalIgnoreCase) ||
                    displayTitle.Contains("lichess", StringComparison.OrdinalIgnoreCase))
                {
                    target.ChessBadge = displayTitle.Contains("lichess", StringComparison.OrdinalIgnoreCase)
                        ? "♟️ Lichess"
                        : "♟️ Chess.com";
                }

                targets.Add(target);
            }
        }
        finally
        {
            NativeMethods.CFRelease(windowList);
        }

        // Generate thumbnails from a single display capture
        GenerateThumbnails(targets);

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

            // One-shot timer pattern: re-arm at end of each tick to prevent overlap
            _captureTimer = new Timer(OnCaptureTimerTick, null, 0, Timeout.Infinite);
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

    /// <summary>
    /// Logs Screen Recording permission status. Does NOT call CGRequestScreenCaptureAccess
    /// because on macOS 15+ (Sequoia) it opens System Settings every launch instead of
    /// showing a one-time dialog. The user must manually enable Screen Recording.
    /// Note: CGPreflightScreenCaptureAccess can return false even when permission IS granted
    /// (especially with development signing) — the real test is whether SCK capture succeeds.
    /// </summary>
    private static void EnsureScreenRecordingPermission()
    {
        var granted = NativeMethods.CGPreflightScreenCaptureAccess();
        Console.WriteLine(
            $"[Capture] Screen Recording preflight: {(granted ? "GRANTED" : "unknown (preflight unreliable on Sequoia)")}");
        Console.WriteLine(
            $"[Capture] Capture method: {(_sckAvailable ? "ScreenCaptureKit (GPU-capable)" : "CGDisplayCreateImage (legacy, no GPU content)")}");
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
            RefreshWindowBounds(target);

            byte[] frameData;
            if (_sckAvailable)
            {
                Console.WriteLine($"[Capture] Attempting SCK capture for windowId={target.Handle} ({target.ProcessName}: {target.WindowTitle}) bounds={target.BoundsWidth}x{target.BoundsHeight}");
                frameData = CaptureWithScreenCaptureKit(target);
                if (frameData.Length == 0)
                {
                    Console.WriteLine("[Capture] SCK returned empty — falling back to CGDisplayCreateImage (CANNOT capture GPU/Metal content)");
                    frameData = CaptureDisplayAndCrop(target);
                    Console.WriteLine($"[Capture] CGDisplayCreateImage fallback returned {frameData.Length} bytes");
                }
                else
                {
                    Console.WriteLine($"[Capture] SCK capture SUCCESS — {frameData.Length} bytes");
                }
            }
            else
            {
                Console.WriteLine($"[Capture] Using CGDisplayCreateImage (SCK not available) for windowId={target.Handle}");
                frameData = CaptureDisplayAndCrop(target);
                Console.WriteLine($"[Capture] CGDisplayCreateImage returned {frameData.Length} bytes");
            }

            if (frameData.Length > 0)
            {
                _captureCount++;
                FrameCaptured?.Invoke(this, frameData);
                LogMemoryFootprint($"After capture #{_captureCount} ({frameData.Length / 1024}KB frame)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Capture] Error: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            // Re-arm one-shot timer for next capture (prevents tick overlap)
            lock (_gate)
            {
                if (IsCapturing && !_disposed)
                    _captureTimer?.Change(CaptureIntervalMs, Timeout.Infinite);
            }
        }
    }

    /// <summary>
    /// Checks whether the native SCK helper is available at runtime.
    /// Wrapped in try/catch so a missing or incompatible xcframework
    /// falls back gracefully instead of crashing.
    /// </summary>
    private static bool CheckSckAvailability()
    {
        try
        {
            var available = NativeMethods.sck_is_available();
            Console.WriteLine($"[Capture] SCK availability check: {available}");
            return available;
        }
        catch (DllNotFoundException ex)
        {
            Console.WriteLine($"[Capture] SCK NOT available — DllNotFoundException: {ex.Message}");
            Console.WriteLine("[Capture] The GaimerScreenCapture.xcframework may not be linked. Falling back to CGDisplayCreateImage (cannot see GPU content).");
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            Console.WriteLine($"[Capture] SCK NOT available — EntryPointNotFoundException: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Captures a window using ScreenCaptureKit via the native helper.
    /// Returns PNG bytes, or empty array on failure/timeout.
    /// </summary>
    private static byte[] CaptureWithScreenCaptureKit(CaptureTarget target)
    {
        var windowId = (uint)target.Handle;
        var width = (int)target.BoundsWidth;
        var height = (int)target.BoundsHeight;

        // Use 2x for Retina (SCK captures at pixel resolution)
        width = Math.Max(width * 2, 100);
        height = Math.Max(height * 2, 100);

        Console.WriteLine($"[Capture] SCK: requesting {width}x{height} for windowId={windowId}");

        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timedOut = false;

        NativeMethods.SckCaptureCallback callback = (dataPtr, length) =>
        {
            if (Volatile.Read(ref timedOut)) return;

            if (dataPtr == IntPtr.Zero || length <= 0)
            {
                Console.WriteLine("[Capture] SCK callback: received NULL/empty data (capture failed in Swift)");
                tcs.TrySetResult(Array.Empty<byte>());
                return;
            }

            Console.WriteLine($"[Capture] SCK callback: received {length} bytes of PNG data");
            var buffer = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(dataPtr, buffer, 0, length);
            tcs.TrySetResult(buffer);
        };

        var handle = System.Runtime.InteropServices.GCHandle.Alloc(callback);
        try
        {
            NativeMethods.sck_capture_window(windowId, width, height, callback);

            if (!tcs.Task.Wait(TimeSpan.FromSeconds(10)))
            {
                Volatile.Write(ref timedOut, true);
                Console.WriteLine("[Capture] SCK capture TIMED OUT after 10s — likely deadlock or run loop issue");
                return Array.Empty<byte>();
            }

            return tcs.Task.Result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Capture] SCK exception: {ex.GetType().Name}: {ex.Message}");
            return Array.Empty<byte>();
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Finds the display containing the target window, captures it via CGDisplayCreateImage,
    /// then crops to the window's bounds using SkiaSharp. Returns PNG bytes.
    /// </summary>
    private static byte[] CaptureDisplayAndCrop(CaptureTarget target)
    {
        // Find which display contains this window
        var windowRect = new CGRect
        {
            X = target.BoundsX,
            Y = target.BoundsY,
            Width = target.BoundsWidth,
            Height = target.BoundsHeight
        };

        var displays = new uint[8];
        NativeMethods.CGGetDisplaysWithRect(windowRect, 8, displays, out var displayCount);

        // Fall back to main display if lookup fails
        var displayId = displayCount > 0 ? displays[0] : NativeMethods.CGMainDisplayID();

        var cgImage = NativeMethods.CGDisplayCreateImage(displayId);
        if (cgImage == IntPtr.Zero) return [];

        try
        {
            var imgWidth = (int)NativeMethods.CGImageGetWidth(cgImage);
            var imgHeight = (int)NativeMethods.CGImageGetHeight(cgImage);
            if (imgWidth == 0 || imgHeight == 0) return [];

            // Convert CGImage → UIImage → PNG → SKBitmap
            // IMPORTANT: dispose UIImage + NSData to prevent native memory leak (~45MB/tick)
            var cgImageManaged = Runtime.GetINativeObject<CoreGraphics.CGImage>(cgImage, owns: false);
            if (cgImageManaged is null) return [];

            using var uiImage = new UIImage(cgImageManaged);
            using var pngData = uiImage.AsPNG();
            if (pngData is null || pngData.Length == 0) return [];

            using var skBitmap = SKBitmap.Decode(pngData.AsStream());
            if (skBitmap is null) return [];

            // Window bounds are in global screen coords. Convert to display-local coords.
            var displayBounds = NativeMethods.CGDisplayBounds(displayId);
            var scaleX = displayBounds.Width > 0 ? skBitmap.Width / displayBounds.Width : 2.0;
            var scaleY = displayBounds.Height > 0 ? skBitmap.Height / displayBounds.Height : 2.0;

            // Subtract display origin to get local coordinates
            var localX = target.BoundsX - displayBounds.X;
            var localY = target.BoundsY - displayBounds.Y;

            var cropX = (int)(localX * scaleX);
            var cropY = (int)(localY * scaleY);
            var cropW = (int)(target.BoundsWidth * scaleX);
            var cropH = (int)(target.BoundsHeight * scaleY);

            // Clamp to image bounds
            cropX = Math.Clamp(cropX, 0, skBitmap.Width - 1);
            cropY = Math.Clamp(cropY, 0, skBitmap.Height - 1);
            cropW = Math.Min(cropW, skBitmap.Width - cropX);
            cropH = Math.Min(cropH, skBitmap.Height - cropY);

            if (cropW < 10 || cropH < 10) return [];

            using var subset = new SKBitmap();
            if (!skBitmap.ExtractSubset(subset, new SKRectI(cropX, cropY, cropX + cropW, cropY + cropH)))
                return [];

            using var image = SKImage.FromBitmap(subset);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }
        finally
        {
            NativeMethods.CGImageRelease(cgImage);
        }
    }

    /// <summary>
    /// Refreshes window bounds from the current window list.
    /// </summary>
    private static void RefreshWindowBounds(CaptureTarget target)
    {
        var windowList = NativeMethods.CGWindowListCopyWindowInfo(
            CGWindowListOption.OptionOnScreenOnly | CGWindowListOption.ExcludeDesktopElements,
            0);

        if (windowList == IntPtr.Zero) return;

        try
        {
            using var array = Runtime.GetNSObject<NSArray>(windowList);
            if (array is null) return;

            for (nuint i = 0; i < array.Count; i++)
            {
                var dictHandle = array.ValueAt(i);
                using var dict = Runtime.GetNSObject<NSDictionary>(dictHandle);
                if (dict is null) continue;

                var windowId = GetIntValue(dict, _nsKeyWindowNumber);
                if (windowId != (int)target.Handle) continue;

                var bounds = GetBoundsRect(dict, _nsKeyWindowBounds);
                target.BoundsX = bounds.X;
                target.BoundsY = bounds.Y;
                target.BoundsWidth = bounds.Width;
                target.BoundsHeight = bounds.Height;
                return;
            }
        }
        finally
        {
            NativeMethods.CFRelease(windowList);
        }
    }

    /// <summary>
    /// Generates thumbnails for all targets. Groups by display for efficiency.
    /// </summary>
    private static void GenerateThumbnails(List<CaptureTarget> targets)
    {
        foreach (var target in targets)
        {
            try
            {
                var thumbnailData = CaptureDisplayAndCrop(target);
                if (thumbnailData.Length > 0)
                    target.Thumbnail = ImageProcessor.GenerateThumbnail(thumbnailData);
            }
            catch
            {
                // Skip thumbnail on error
            }
        }
    }

    // --- NSDictionary helpers (use pre-allocated NSString keys to avoid per-call allocation) ---

    private static string? GetStringValue(NSDictionary dict, NSString key)
    {
        if (dict.TryGetValue(key, out var val) && val is NSString nsStr)
            return nsStr.ToString();
        return null;
    }

    private static int GetIntValue(NSDictionary dict, NSString key)
    {
        if (dict.TryGetValue(key, out var val) && val is NSNumber num)
            return num.Int32Value;
        return 0;
    }

    private static (double X, double Y, double Width, double Height) GetBoundsRect(NSDictionary dict, NSString key)
    {
        if (!dict.TryGetValue(key, out var val) || val is not NSDictionary bounds)
            return (0, 0, 0, 0);

        double x = 0, y = 0, w = 0, h = 0;
        if (bounds.TryGetValue(_nsKeyX, out var xVal) && xVal is NSNumber xNum)
            x = xNum.DoubleValue;
        if (bounds.TryGetValue(_nsKeyY, out var yVal) && yVal is NSNumber yNum)
            y = yNum.DoubleValue;
        if (bounds.TryGetValue(_nsKeyWidth, out var wVal) && wVal is NSNumber wNum)
            w = wNum.DoubleValue;
        if (bounds.TryGetValue(_nsKeyHeight, out var hVal) && hVal is NSNumber hNum)
            h = hNum.DoubleValue;
        return (x, y, w, h);
    }

    private static void LogMemoryFootprint(string context)
    {
        var managed = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var working = process.WorkingSet64 / (1024.0 * 1024.0);
        var priv = process.PrivateMemorySize64 / (1024.0 * 1024.0);
        Console.WriteLine($"[Memory] {context} — Managed: {managed:F1}MB | WorkingSet: {working:F1}MB | Private: {priv:F1}MB");
    }
}
