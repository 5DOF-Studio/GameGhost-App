// GhostPanel.swift
// NSPanel subclass for the ghost mode overlay.
//
// Creates a transparent, borderless, non-activating floating panel
// that sits above all windows including fullscreen games.
// Click-through for transparent areas is handled by GhostPanelContentView.
//
// PURE APPKIT — no SwiftUI.

import AppKit

// MARK: - GhostPanel

/// A floating NSPanel configured for overlay use.
/// - Borderless and non-activating (does not steal focus from the game)
/// - Transparent background (only subviews are visible)
/// - Floating level (sits above normal windows)
/// - Joins all Spaces and works alongside fullscreen apps
class GhostPanel: NSPanel {

    override init(
        contentRect: NSRect,
        styleMask style: NSWindow.StyleMask,
        backing backingStoreType: NSWindow.BackingStoreType,
        defer flag: Bool
    ) {
        super.init(
            contentRect: contentRect,
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        // Transparency
        isOpaque = false
        backgroundColor = .clear
        hasShadow = false

        // Floating behavior
        level = .floating
        isFloatingPanel = true

        // Multi-space and fullscreen support
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]

        // Misc
        isExcludedFromWindowsMenu = true
        isReleasedWhenClosed = false
        hidesOnDeactivate = false
    }

    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }

    /// Sets a GhostPanelContentView as the panel's content.
    func setupContent(_ view: GhostPanelContentView) {
        view.frame = contentRect(forFrameRect: frame)
        view.autoresizingMask = [.width, .height]
        contentView = view
    }
}
