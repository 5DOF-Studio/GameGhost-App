// GaimerGhostMode.swift
// C-callable exports for the ghost mode overlay panel.
//
// Exports 14 @_cdecl functions consumed by .NET MAUI via DllImport/P/Invoke:
//   - ghost_panel_create/destroy/show/hide
//   - ghost_panel_set_agent_image/fab_active/fab_connected
//   - ghost_panel_show_card/dismiss_card
//   - ghost_panel_set_fab_tap_callback/card_dismiss_callback
//   - ghost_panel_set_position/set_size
//
// CRITICAL: Uses DispatchQueue.main.async (NOT @MainActor) for all UI operations
// to avoid deadlock when C# blocks with Task.Wait(). Lesson from Phase 03.
//
// CRITICAL: String parameters (UnsafePointer<CChar>) are converted to Swift String
// immediately before dispatching to main queue, because the pointer may be freed
// by .NET after the @_cdecl function returns.

import Foundation
import AppKit

// MARK: - Singleton State

/// Shared panel instance (created by ghost_panel_create, destroyed by ghost_panel_destroy).
var sharedPanel: GhostPanel?

/// Shared observable state driving the SwiftUI views.
var sharedState: GhostPanelState?

/// Stored C function pointer callbacks (pinned by GCHandle on the C# side).
var fabTapCallback: (@convention(c) () -> Void)?
var cardDismissCallback: (@convention(c) () -> Void)?

// MARK: - Panel Lifecycle

/// Creates the ghost panel and sets up the SwiftUI content view.
/// Call once at app startup. Returns true on success.
///
/// Uses DispatchQueue.main.sync since this is called once at init from a background thread.
/// The C# side should NOT call this from the main thread.
@_cdecl("ghost_panel_create")
public func ghostPanelCreate() -> Bool {
    NSLog("[GaimerGhostMode] ghost_panel_create called (mainThread=%d)", Thread.isMainThread ? 1 : 0)

    // Must run on main thread for AppKit, but DispatchQueue.main.sync deadlocks
    // if we're already on the main thread. Use the helper to handle both cases.
    var success = false
    runOnMainSync {
        if sharedPanel != nil {
            NSLog("[GaimerGhostMode] Panel already exists, returning true")
            success = true
            return
        }

        let state = GhostPanelState()
        let contentView = GhostPanelContentView(state: state)

        // Default panel size -- covers a reasonable area for FAB + cards
        let screenFrame = NSScreen.main?.visibleFrame ?? NSRect(x: 0, y: 0, width: 400, height: 600)
        let panelFrame = NSRect(
            x: screenFrame.maxX - 340,
            y: screenFrame.minY,
            width: 340,
            height: screenFrame.height
        )

        let panel = GhostPanel(
            contentRect: panelFrame,
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        panel.setupContent(contentView)

        sharedPanel = panel
        sharedState = state
        success = true

        NSLog("[GaimerGhostMode] Panel created: frame=%@", NSStringFromRect(panelFrame))
    }

    return success
}

/// Executes a closure synchronously on the main thread.
/// If already on the main thread, runs directly to avoid deadlock.
private func runOnMainSync(_ block: () -> Void) {
    if Thread.isMainThread {
        block()
    } else {
        DispatchQueue.main.sync(execute: block)
    }
}

/// Destroys the ghost panel and releases all references.
@_cdecl("ghost_panel_destroy")
public func ghostPanelDestroy() {
    NSLog("[GaimerGhostMode] ghost_panel_destroy called")

    DispatchQueue.main.async {
        sharedPanel?.orderOut(nil)
        sharedPanel?.close()
        sharedPanel = nil
        sharedState = nil
        fabTapCallback = nil
        cardDismissCallback = nil
        NSLog("[GaimerGhostMode] Panel destroyed")
    }
}

/// Shows the ghost panel (brings to front).
@_cdecl("ghost_panel_show")
public func ghostPanelShow() {
    NSLog("[GaimerGhostMode] ghost_panel_show called")

    DispatchQueue.main.async {
        sharedPanel?.orderFront(nil)
    }
}

/// Hides the ghost panel.
@_cdecl("ghost_panel_hide")
public func ghostPanelHide() {
    NSLog("[GaimerGhostMode] ghost_panel_hide called")

    DispatchQueue.main.async {
        sharedPanel?.orderOut(nil)
    }
}

// MARK: - FAB State

/// Sets the agent portrait image path displayed on the FAB.
@_cdecl("ghost_panel_set_agent_image")
public func ghostPanelSetAgentImage(pathPtr: UnsafePointer<CChar>) {
    // Convert string immediately before dispatching (pointer freed by .NET after return)
    let path = String(cString: pathPtr)
    NSLog("[GaimerGhostMode] ghost_panel_set_agent_image: %@", path)

    DispatchQueue.main.async {
        sharedState?.agentImagePath = path
    }
}

/// Sets whether the FAB is in active (ghost mode engaged) state.
@_cdecl("ghost_panel_set_fab_active")
public func ghostPanelSetFabActive(active: Bool) {
    NSLog("[GaimerGhostMode] ghost_panel_set_fab_active: %d", active ? 1 : 0)

    DispatchQueue.main.async {
        sharedState?.isFabActive = active
    }
}

/// Sets whether the FAB shows connected state (yellow glow).
@_cdecl("ghost_panel_set_fab_connected")
public func ghostPanelSetFabConnected(connected: Bool) {
    NSLog("[GaimerGhostMode] ghost_panel_set_fab_connected: %d", connected ? 1 : 0)

    DispatchQueue.main.async {
        sharedState?.isFabConnected = connected
    }
}

// MARK: - Card State

/// Shows an event card with the given variant and content.
/// Variants: 1=Voice, 2=Text, 3=TextWithImage
@_cdecl("ghost_panel_show_card")
public func ghostPanelShowCard(
    variant: Int32,
    titlePtr: UnsafePointer<CChar>?,
    textPtr: UnsafePointer<CChar>?,
    imagePathPtr: UnsafePointer<CChar>?
) {
    // Convert all strings immediately before dispatching
    let title: String? = titlePtr.map { String(cString: $0) }
    let text: String? = textPtr.map { String(cString: $0) }
    let imagePath: String? = imagePathPtr.map { String(cString: $0) }

    NSLog("[GaimerGhostMode] ghost_panel_show_card: variant=%d, title=%@, text=%@",
          variant, title ?? "(nil)", text ?? "(nil)")

    DispatchQueue.main.async {
        sharedState?.cardTitle = title
        sharedState?.cardText = text
        sharedState?.cardImagePath = imagePath
        sharedState?.cardVariant = variant
    }
}

/// Dismisses the currently displayed event card.
@_cdecl("ghost_panel_dismiss_card")
public func ghostPanelDismissCard() {
    NSLog("[GaimerGhostMode] ghost_panel_dismiss_card called")

    DispatchQueue.main.async {
        sharedState?.cardVariant = 0
    }
}

// MARK: - Callbacks

/// Registers a C function pointer to be called when the user taps the FAB.
/// The C# side must pin this delegate with GCHandle.Alloc.
@_cdecl("ghost_panel_set_fab_tap_callback")
public func ghostPanelSetFabTapCallback(callback: @convention(c) () -> Void) {
    NSLog("[GaimerGhostMode] ghost_panel_set_fab_tap_callback registered")

    // Store the raw function pointer
    fabTapCallback = callback

    DispatchQueue.main.async {
        sharedState?.onFabTap = {
            // Invoke the C function pointer (called from main thread)
            fabTapCallback?()
        }
    }
}

/// Registers a C function pointer to be called when a card is dismissed.
/// The C# side must pin this delegate with GCHandle.Alloc.
@_cdecl("ghost_panel_set_card_dismiss_callback")
public func ghostPanelSetCardDismissCallback(callback: @convention(c) () -> Void) {
    NSLog("[GaimerGhostMode] ghost_panel_set_card_dismiss_callback registered")

    // Store the raw function pointer
    cardDismissCallback = callback

    DispatchQueue.main.async {
        sharedState?.onCardDismiss = {
            // Invoke the C function pointer (called from main thread)
            cardDismissCallback?()
        }
    }
}

// MARK: - Positioning

/// Sets the panel's origin position (bottom-left corner in screen coordinates).
@_cdecl("ghost_panel_set_position")
public func ghostPanelSetPosition(x: Double, y: Double) {
    NSLog("[GaimerGhostMode] ghost_panel_set_position: (%f, %f)", x, y)

    DispatchQueue.main.async {
        sharedPanel?.setFrameOrigin(NSPoint(x: x, y: y))
    }
}

/// Sets the panel's content size.
@_cdecl("ghost_panel_set_size")
public func ghostPanelSetSize(width: Double, height: Double) {
    NSLog("[GaimerGhostMode] ghost_panel_set_size: (%f x %f)", width, height)

    DispatchQueue.main.async {
        sharedPanel?.setContentSize(NSSize(width: width, height: height))
    }
}

// MARK: - Host Window Management

/// Stored reference to the host (MAUI) NSWindow so we can restore it.
var hiddenHostWindow: NSWindow?

/// Hides the main app (MAUI/Catalyst) NSWindow at the AppKit level.
/// UIWindow.Hidden only hides the UIKit layer but leaves the NSWindow chrome
/// (title bar + empty content area) visible. This function hides the entire NSWindow.
@_cdecl("ghost_panel_hide_host_window")
public func ghostPanelHideHostWindow() {
    NSLog("[GaimerGhostMode] ghost_panel_hide_host_window called")

    DispatchQueue.main.async {
        // Find the main app window (not our ghost panel)
        for window in NSApplication.shared.windows {
            if window !== sharedPanel && window.isVisible && window.title.contains("Gaimer") || (window !== sharedPanel && window.isVisible && window.className.contains("UINSWindow")) {
                NSLog("[GaimerGhostMode] Hiding host window: %@ (class=%@)", window.title, window.className)
                hiddenHostWindow = window
                window.orderOut(nil)
                return
            }
        }
        NSLog("[GaimerGhostMode] No host window found to hide")
    }
}

/// Restores the previously hidden host window.
@_cdecl("ghost_panel_show_host_window")
public func ghostPanelShowHostWindow() {
    NSLog("[GaimerGhostMode] ghost_panel_show_host_window called")

    DispatchQueue.main.async {
        if let window = hiddenHostWindow {
            NSLog("[GaimerGhostMode] Restoring host window: %@", window.title)
            window.makeKeyAndOrderFront(nil)
            hiddenHostWindow = nil
        } else {
            // Fallback: find any UINSWindow and show it
            for window in NSApplication.shared.windows {
                if window !== sharedPanel && window.className.contains("UINSWindow") {
                    NSLog("[GaimerGhostMode] Restoring fallback host window: %@", window.title)
                    window.makeKeyAndOrderFront(nil)
                    return
                }
            }
            NSLog("[GaimerGhostMode] No host window to restore")
        }
    }
}
