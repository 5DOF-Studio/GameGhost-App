using System.Runtime.InteropServices;
using WitnessDesktop.Models;
using WitnessDesktop.Services;

namespace WitnessDesktop.Platforms.MacCatalyst;

/// <summary>
/// macOS implementation of <see cref="IGhostModeService"/> that bridges to the
/// native GaimerGhostMode xcframework via P/Invoke. Uses the same interop pattern
/// as WindowCaptureService: @_cdecl exports + DllImport + GCHandle-pinned callbacks.
/// </summary>
public class MacGhostModeService : IGhostModeService, IDisposable
{
    private readonly bool _isSupported;
    private bool _isGhostModeActive;
    private bool _disposed;

    // Callback delegates must be stored as fields AND pinned with GCHandle
    // to prevent GC collection while native code holds function pointers.
    private GhostModeNativeMethods.GhostModeCallback? _fabTapCallback;
    private GhostModeNativeMethods.GhostModeCallback? _cardDismissCallback;
    private GCHandle _fabTapHandle;
    private GCHandle _cardDismissHandle;

    public bool IsGhostModeActive => _isGhostModeActive;
    public bool IsSupported => _isSupported;

    public event EventHandler? FabTapped;
    public event EventHandler? CardDismissed;

    public MacGhostModeService()
    {
        try
        {
            _isSupported = GhostModeNativeMethods.ghost_panel_create();
        }
        catch (DllNotFoundException ex)
        {
            Console.WriteLine($"[MacGhostModeService] GaimerGhostMode framework not found: {ex.Message}");
            _isSupported = false;
            return;
        }
        catch (EntryPointNotFoundException ex)
        {
            Console.WriteLine($"[MacGhostModeService] ghost_panel_create not found: {ex.Message}");
            _isSupported = false;
            return;
        }

        if (!_isSupported)
        {
            Console.WriteLine("[MacGhostModeService] ghost_panel_create returned false, ghost mode unavailable");
            return;
        }

        Console.WriteLine("[MacGhostModeService] Ghost panel created successfully");

        // Create managed delegates for native callbacks
        _fabTapCallback = OnFabTapped;
        _cardDismissCallback = OnCardDismissed;

        // Pin delegates to prevent GC collection while native code holds function pointers
        _fabTapHandle = GCHandle.Alloc(_fabTapCallback);
        _cardDismissHandle = GCHandle.Alloc(_cardDismissCallback);

        // Register callbacks with native layer
        GhostModeNativeMethods.ghost_panel_set_fab_tap_callback(_fabTapCallback);
        GhostModeNativeMethods.ghost_panel_set_card_dismiss_callback(_cardDismissCallback);
    }

    private void OnFabTapped()
    {
        FabTapped?.Invoke(this, EventArgs.Empty);
    }

    private void OnCardDismissed()
    {
        CardDismissed?.Invoke(this, EventArgs.Empty);
    }

    public Task EnterGhostModeAsync()
    {
        if (!_isSupported || _isGhostModeActive)
            return Task.CompletedTask;

        _isGhostModeActive = true;

        // Hide the main app window at the AppKit/NSWindow level.
        // UIKit UIWindow.Hidden only hides the content layer — the NSWindow chrome
        // (title bar + empty dark content area) stays visible. The native function
        // uses NSWindow.orderOut to fully remove it from screen.
        GhostModeNativeMethods.ghost_panel_hide_host_window();

        GhostModeNativeMethods.ghost_panel_show();
        Console.WriteLine("[MacGhostModeService] Ghost panel shown, ghost mode active");

        return Task.CompletedTask;
    }

    public Task ExitGhostModeAsync()
    {
        if (!_isSupported || !_isGhostModeActive)
            return Task.CompletedTask;

        GhostModeNativeMethods.ghost_panel_hide();
        Console.WriteLine("[MacGhostModeService] Ghost panel hidden");

        // Restore the host NSWindow at the AppKit level
        GhostModeNativeMethods.ghost_panel_show_host_window();
        Console.WriteLine("[MacGhostModeService] MAUI window restored");

        _isGhostModeActive = false;
        Console.WriteLine("[MacGhostModeService] Ghost mode deactivated");

        return Task.CompletedTask;
    }

    public void SetAgentImage(string imagePath)
    {
        if (!_isSupported) return;

        GhostModeNativeMethods.ghost_panel_set_agent_image(imagePath);
    }

    public void SetFabState(bool active, bool connected)
    {
        if (!_isSupported) return;

        GhostModeNativeMethods.ghost_panel_set_fab_active(active);
        GhostModeNativeMethods.ghost_panel_set_fab_connected(connected);
    }

    public void ShowCard(FabCardVariant variant, string? title, string? text, string? imagePath)
    {
        if (!_isSupported) return;

        GhostModeNativeMethods.ghost_panel_show_card((int)variant, title, text, imagePath);
    }

    public void DismissCard()
    {
        if (!_isSupported) return;

        GhostModeNativeMethods.ghost_panel_dismiss_card();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        // Destroy the native panel FIRST — this unregisters callback pointers.
        // Freeing GCHandles before destroy risks use-after-free if a callback fires
        // between handle free and panel destruction (e.g., auto-dismiss timer).
        if (_isSupported)
        {
            try
            {
                GhostModeNativeMethods.ghost_panel_destroy();
                Console.WriteLine("[MacGhostModeService] Ghost panel destroyed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MacGhostModeService] Error destroying ghost panel: {ex.Message}");
            }
        }

        // Now safe to free pinned callback handles
        if (_fabTapHandle.IsAllocated)
            _fabTapHandle.Free();

        if (_cardDismissHandle.IsAllocated)
            _cardDismissHandle.Free();

        _disposed = true;
    }

    ~MacGhostModeService()
    {
        Dispose(false);
    }
}
