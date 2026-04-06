using System.Reflection;
using System.Runtime.InteropServices;
using ObjCRuntime;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// P/Invoke declarations for CoreGraphics window capture functions.
/// These functions have no managed bindings in the MAUI/Catalyst SDK.
/// </summary>
internal static class NativeMethods
{
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string SckLibName = "GaimerScreenCapture";
    private const string GhostModeLibName = "GaimerGhostMode";
    private const string SpeechLibName = "GaimerSpeech";

    /// <summary>
    /// Register a DllImport resolver so .NET can find custom xcframeworks
    /// inside the app bundle's Contents/Frameworks/ directory. Without this, DllImport
    /// throws DllNotFoundException because it doesn't search @rpath framework paths.
    /// Handles both GaimerScreenCapture and GaimerGhostMode from a single registration
    /// (NativeLibrary.SetDllImportResolver can only be called once per assembly).
    /// </summary>
    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, ResolveDllImport);
    }

    private static IntPtr ResolveDllImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == SckLibName)
            return ResolveFramework("GaimerScreenCapture");

        if (libraryName == GhostModeLibName)
            return ResolveFramework("GaimerGhostMode");

        if (libraryName == SpeechLibName)
            return ResolveFramework("GaimerSpeech");

        return IntPtr.Zero; // Let default resolution handle other libraries
    }

    // dlopen/dlerror for diagnostic logging when NativeLibrary.TryLoad fails
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlopen")]
    private static extern IntPtr dlopen_raw(string path, int mode);

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlerror")]
    private static extern IntPtr dlerror_raw();

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlclose")]
    private static extern int dlclose_raw(IntPtr handle);

    private static string? GetDlError()
    {
        var ptr = dlerror_raw();
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
    }

    /// <summary>
    /// Resolves a native framework by name from the app bundle's Contents/Frameworks/ directory.
    /// Tries both the flat path and the versioned (Versions/A/) path that xcframeworks may use.
    /// </summary>
    private static IntPtr ResolveFramework(string frameworkName)
    {
        // The framework is at: <AppBundle>/Contents/Frameworks/<Name>.framework/<Name>
        var appBundle = Foundation.NSBundle.MainBundle.BundlePath;
        var frameworkPath = System.IO.Path.Combine(
            appBundle, "Contents", "Frameworks",
            $"{frameworkName}.framework", frameworkName);

        Console.WriteLine($"[NativeMethods] Resolving {frameworkName} → {frameworkPath}");

        if (NativeLibrary.TryLoad(frameworkPath, out var handle))
        {
            Console.WriteLine($"[NativeMethods] Successfully loaded {frameworkName} framework");
            return handle;
        }

        // Get the real dlopen error for diagnostics
        dlerror_raw(); // clear previous error
        var rawHandle = dlopen_raw(frameworkPath, 0x0002 /* RTLD_NOW */);
        var err = GetDlError();
        Console.WriteLine($"[NativeMethods] dlopen failed for {frameworkName}: {err ?? "unknown"}");
        if (rawHandle != IntPtr.Zero) dlclose_raw(rawHandle);

        // Try versioned path (xcframework uses Versions/A/ structure)
        var versionedPath = System.IO.Path.Combine(
            appBundle, "Contents", "Frameworks",
            $"{frameworkName}.framework", "Versions", "A", frameworkName);

        Console.WriteLine($"[NativeMethods] Trying versioned path: {versionedPath}");

        if (NativeLibrary.TryLoad(versionedPath, out handle))
        {
            Console.WriteLine($"[NativeMethods] Successfully loaded {frameworkName} framework (versioned)");
            return handle;
        }

        dlerror_raw();
        rawHandle = dlopen_raw(versionedPath, 0x0002);
        err = GetDlError();
        Console.WriteLine($"[NativeMethods] dlopen failed for {frameworkName} (versioned): {err ?? "unknown"}");
        if (rawHandle != IntPtr.Zero) dlclose_raw(rawHandle);

        Console.WriteLine($"[NativeMethods] FAILED to load {frameworkName} from either path");
        return IntPtr.Zero;
    }

    // --- CGWindowList functions ---

    /// <summary>
    /// Returns an array of CFDictionary objects describing on-screen windows.
    /// Caller must release the returned CFArray with CFRelease.
    /// </summary>
    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGWindowListCopyWindowInfo(CGWindowListOption option, uint relativeToWindow);

    /// <summary>
    /// Creates a composite image of the specified windows.
    /// Caller must release the returned CGImage with CGImageRelease.
    /// </summary>
    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGWindowListCreateImage(
        CGRect screenBounds,
        CGWindowListOption listOption,
        uint windowId,
        CGWindowImageOption imageOption);

    [DllImport(CoreGraphicsLib)]
    public static extern void CGImageRelease(IntPtr image);

    [DllImport(CoreGraphicsLib)]
    public static extern nint CGImageGetWidth(IntPtr image);

    [DllImport(CoreGraphicsLib)]
    public static extern nint CGImageGetHeight(IntPtr image);

    // --- CoreFoundation helpers ---

    [DllImport(CoreFoundationLib)]
    public static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundationLib)]
    public static extern nint CFArrayGetCount(IntPtr theArray);

    [DllImport(CoreFoundationLib)]
    public static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, nint idx);

    // --- Display capture ---

    [DllImport(CoreGraphicsLib)]
    public static extern uint CGMainDisplayID();

    [DllImport(CoreGraphicsLib)]
    public static extern CGRect CGDisplayBounds(uint display);

    /// <summary>
    /// Captures the entire display as a CGImage. Not deprecated (unlike CGWindowListCreateImage).
    /// Caller must release with CGImageRelease.
    /// </summary>
    [DllImport(CoreGraphicsLib)]
    public static extern IntPtr CGDisplayCreateImage(uint displayId);

    /// <summary>
    /// Returns the list of displays whose bounds intersect the given rect.
    /// </summary>
    [DllImport(CoreGraphicsLib)]
    public static extern int CGGetDisplaysWithRect(
        CGRect rect, uint maxDisplays,
        [Out] uint[] displays, out uint matchingDisplayCount);

    // --- Screen Recording permission (macOS 10.15+) ---

    /// <summary>Returns true if Screen Recording permission is already granted.</summary>
    [DllImport(CoreGraphicsLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CGPreflightScreenCaptureAccess();

    /// <summary>
    /// Triggers the Screen Recording permission dialog if not yet granted.
    /// Returns true if permission was already granted (does NOT wait for user response).
    /// </summary>
    [DllImport(CoreGraphicsLib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CGRequestScreenCaptureAccess();

    // --- ScreenCaptureKit native helper (GaimerScreenCapture.xcframework) ---

    /// <summary>
    /// Callback delegate for sck_capture_window. Receives PNG bytes (data, length)
    /// or (IntPtr.Zero, 0) on failure. The callback fires on the main thread.
    /// IMPORTANT: The caller must pin this delegate (e.g. via GCHandle.Alloc) for the
    /// duration of the native call to prevent GC collection.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void SckCaptureCallback(IntPtr data, int length);

    /// <summary>Returns true if ScreenCaptureKit is available (macCatalyst 18.2+ / macOS 14+).</summary>
    [DllImport("GaimerScreenCapture", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool sck_is_available();

    /// <summary>
    /// Captures a window via ScreenCaptureKit. Calls back with PNG bytes (data, length)
    /// or (IntPtr.Zero, 0) on failure. The callback fires on the main thread.
    /// </summary>
    [DllImport("GaimerScreenCapture", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void sck_capture_window(
        uint windowID, int width, int height,
        SckCaptureCallback callback);

    // --- ScreenCaptureKit recording (GaimerScreenCapture.xcframework) ---

    /// <summary>
    /// Callback delegate for sck_stop_recording. Fires on a background thread
    /// when the recording file is finalized and safe to read.
    /// Pin via GCHandle.Alloc for the duration of the native call.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void SckRecordingStoppedCallback();

    /// <summary>
    /// Start continuous recording of a window to an HEVC MP4 file.
    /// Returns true if recording started successfully.
    /// </summary>
    [DllImport("GaimerScreenCapture", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool sck_start_recording(
        uint windowID,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath,
        int width,
        int height);

    /// <summary>
    /// Stop recording. Calls the callback when the file is finalized.
    /// The callback fires on a background thread — use TaskCompletionSource on C# side.
    /// </summary>
    [DllImport("GaimerScreenCapture", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void sck_stop_recording(SckRecordingStoppedCallback completion);

    /// <summary>
    /// Returns recording status: 0=idle, 1=recording, 2=error.
    /// </summary>
    [DllImport("GaimerScreenCapture", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sck_recording_status();

    // --- ScreenCaptureKit segment rotation (GaimerScreenCapture.xcframework) ---

    /// <summary>
    /// Callback delegate for sck_rotate_segment. Fires on a background thread
    /// when the OLD segment file is finalized and safe to read.
    /// Pin via GCHandle.Alloc for the duration of the native call.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void SckRotationCompletedCallback();

    /// <summary>
    /// Rotate to a new segment file. The current segment is finalized asynchronously.
    /// The callback fires when the old file is safe to read.
    /// </summary>
    [DllImport("GaimerScreenCapture", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void sck_rotate_segment(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newOutputPath,
        SckRotationCompletedCallback completion);
}

/// <summary>Options for CGWindowListCopyWindowInfo / CGWindowListCreateImage.</summary>
[Flags]
public enum CGWindowListOption : uint
{
    OptionAll = 0,
    OptionOnScreenOnly = (1 << 0),
    OptionOnScreenAboveWindow = (1 << 1),
    OptionOnScreenBelowWindow = (1 << 2),
    OptionIncludingWindow = (1 << 3),
    ExcludeDesktopElements = (1 << 4)
}

/// <summary>Options for CGWindowListCreateImage.</summary>
[Flags]
public enum CGWindowImageOption : uint
{
    Default = 0,
    BoundsIgnoreFraming = (1 << 0),
    ShouldBeOpaque = (1 << 1),
    OnlyShadows = (1 << 2),
    BestResolution = (1 << 3),
    NominalResolution = (1 << 4)
}

/// <summary>CGRect struct matching the CoreGraphics layout (origin + size).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CGRect
{
    public double X;
    public double Y;
    public double Width;
    public double Height;

    public static readonly CGRect Null = new() { X = double.PositiveInfinity, Y = double.PositiveInfinity, Width = 0, Height = 0 };
    public static readonly CGRect Infinite = new() { X = double.NegativeInfinity, Y = double.NegativeInfinity, Width = double.PositiveInfinity, Height = double.PositiveInfinity };
}
