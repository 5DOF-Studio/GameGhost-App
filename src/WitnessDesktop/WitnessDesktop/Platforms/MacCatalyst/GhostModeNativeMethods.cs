using System.Runtime.InteropServices;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// P/Invoke declarations for GaimerGhostMode native Swift helper.
/// All 14 ghost_panel_* functions exported via @_cdecl from the xcframework.
/// The DllImportResolver in NativeMethods.cs handles library resolution for
/// both GaimerScreenCapture and GaimerGhostMode from a single registration.
/// </summary>
internal static class GhostModeNativeMethods
{
    private const string LibName = "GaimerGhostMode";

    /// <summary>
    /// Ensures the DllImportResolver in NativeMethods.cs is registered before
    /// any DllImport in this class is resolved. Without this, if GhostModeNativeMethods
    /// is accessed before NativeMethods, the resolver won't exist and DllImport
    /// for "GaimerGhostMode" will throw DllNotFoundException.
    /// </summary>
    static GhostModeNativeMethods()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(NativeMethods).TypeHandle);
    }

    /// <summary>
    /// Callback delegate for FAB tap and card dismiss events.
    /// Must be pinned with GCHandle.Alloc to prevent GC collection
    /// while native code holds the function pointer.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void GhostModeCallback();

    // --- Panel lifecycle ---

    /// <summary>Creates the native ghost mode panel. Returns true on success.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool ghost_panel_create();

    /// <summary>Destroys the native ghost mode panel and frees resources.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_destroy();

    // --- Visibility ---

    /// <summary>Shows the ghost mode panel (FAB becomes visible).</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_show();

    /// <summary>Hides the ghost mode panel.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_hide();

    // --- FAB configuration ---

    /// <summary>Sets the agent image displayed on the FAB.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_agent_image(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    /// <summary>Sets the FAB active state (pulsing glow when active).</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_fab_active(
        [MarshalAs(UnmanagedType.U1)] bool active);

    /// <summary>Sets the FAB connected state (yellow ring when connected).</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_fab_connected(
        [MarshalAs(UnmanagedType.U1)] bool connected);

    // --- Event cards ---

    /// <summary>Shows an event card attached to the FAB.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_show_card(
        int variant,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? title,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? text,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? imagePath);

    /// <summary>Dismisses the currently visible event card.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_dismiss_card();

    // --- Callbacks ---

    /// <summary>Registers a callback invoked when the user taps the FAB.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_fab_tap_callback(GhostModeCallback callback);

    /// <summary>Registers a callback invoked when an event card is dismissed.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_card_dismiss_callback(GhostModeCallback callback);

    // --- Layout ---

    /// <summary>Sets the position of the ghost mode panel on screen.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_position(double x, double y);

    /// <summary>Sets the size of the ghost mode panel.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_size(double width, double height);

    // --- Host window management ---

    /// <summary>Hides the main app NSWindow at the AppKit level (not just UIKit Hidden).</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_hide_host_window();

    /// <summary>Restores the previously hidden host NSWindow.</summary>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_show_host_window();
}
