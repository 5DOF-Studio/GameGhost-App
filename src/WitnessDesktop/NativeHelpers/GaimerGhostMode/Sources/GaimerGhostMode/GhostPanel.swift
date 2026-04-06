//
//  GhostPanel.swift
//  GhostFab-AppKit
//
//  Borderless floating NSPanel for ghost mode overlay.
//  Floats above fullscreen game windows without stealing focus.
//

import AppKit

public class GhostPanel: NSPanel {
    private static let savedPlacementsKey = "GaimerGhostMode.SavedPanelPlacements"

    private struct SavedPlacement {
        let leftX: CGFloat
        let topY: CGFloat
    }

    // MARK: - Factory

    /// Creates a fully configured GhostPanel ready for overlay use.
    public static func create() -> GhostPanel {
        let panel = GhostPanel(
            contentRect: NSRect(
                x: 100,
                y: 100,
                width: GhostFabTokens.CaseWidth,
                height: GhostFabTokens.BarHeight * 2 + GhostFabTokens.SeparatorThickness
            ),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        // --- Window behavior ---
        panel.level = .floating
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.isMovableByWindowBackground = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.hidesOnDeactivate = false
        panel.ignoresMouseEvents = false
        panel.isReleasedWhenClosed = false
        panel.isExcludedFromWindowsMenu = true

        // --- Root content view ---
        let contentView = GhostContentView(
            frame: panel.contentRect(forFrameRect: panel.frame)
        )
        panel.contentView = contentView

        return panel
    }

    // MARK: - Focus Prevention

    override public var canBecomeKey: Bool { true }
    override public var canBecomeMain: Bool { false }

    // MARK: - Public API

    /// Moves the panel origin to the given screen coordinate.
    public func reposition(x: CGFloat, y: CGFloat) {
        self.setFrameOrigin(NSPoint(x: x, y: y))
    }

    /// Resizes the panel while preserving the current top edge position.
    /// Used by the animated spine layout so the ghost stays anchored to the
    /// top-right of the target display instead of collapsing toward the bottom.
    public func resizePreservingTop(width: CGFloat, height: CGFloat) {
        let currentTopY = frame.maxY
        let nextOrigin = NSPoint(x: frame.origin.x, y: currentTopY - height)
        self.setFrame(
            NSRect(x: nextOrigin.x, y: nextOrigin.y, width: width, height: height),
            display: true
        )
        self.contentView?.frame = NSRect(x: 0, y: 0, width: width, height: height)
    }

    /// Resizes the panel and its content view to the given dimensions.
    public func resize(width: CGFloat, height: CGFloat) {
        let origin = self.frame.origin
        self.setFrame(
            NSRect(x: origin.x, y: origin.y, width: width, height: height),
            display: true
        )
        self.contentView?.frame = NSRect(x: 0, y: 0, width: width, height: height)
    }

    /// Brings the panel to front regardless of app activation state.
    /// Uses `orderFrontRegardless` because the host app may not be active
    /// during gameplay.
    public func showPanel() {
        applySavedPlacementIfAvailable()
        self.orderFrontRegardless()
    }

    /// Hides the panel without closing it.
    public func hidePanel() {
        self.orderOut(nil)
    }

    /// Moves the panel during a user drag, clamped to the visible frame of the
    /// screen currently under the pointer.
    public func repositionForDrag(proposedOrigin: CGPoint) {
        let screen = screenForDragMovement() ?? screenForCurrentFrame()
        let clampedOrigin = clamp(origin: proposedOrigin, in: screen)
        setFrameOrigin(clampedOrigin)
    }

    /// Persists the current placement for the display the panel is presently on.
    public func persistCurrentPlacement() {
        guard let screen = screenForCurrentFrame(),
              let screenNumber = screenNumber(for: screen) else { return }

        var placements = loadSavedPlacements()
        placements[String(screenNumber)] = [
            "leftX": Double(frame.origin.x),
            "topY": Double(frame.maxY)
        ]
        UserDefaults.standard.set(placements, forKey: Self.savedPlacementsKey)
    }

    private func applySavedPlacementIfAvailable() {
        guard let targetScreen = screenForCurrentFrame() else { return }

        let effectiveOrigin: CGPoint
        if let screenNumber = screenNumber(for: targetScreen),
           let savedPlacement = savedPlacement(for: screenNumber) {
            effectiveOrigin = clampedOrigin(
                leftX: savedPlacement.leftX,
                topY: savedPlacement.topY,
                in: targetScreen
            )
        } else {
            effectiveOrigin = clampedOrigin(
                leftX: frame.origin.x,
                topY: targetScreen.visibleFrame.maxY,
                in: targetScreen
            )
        }

        if effectiveOrigin != frame.origin {
            setFrameOrigin(effectiveOrigin)
        }
    }

    private func savedPlacement(for screenNumber: Int) -> SavedPlacement? {
        let placements = loadSavedPlacements()
        guard let payload = placements[String(screenNumber)],
              let leftX = payload["leftX"],
              let topY = payload["topY"] else {
            return nil
        }

        return SavedPlacement(leftX: CGFloat(leftX), topY: CGFloat(topY))
    }

    private func loadSavedPlacements() -> [String: [String: Double]] {
        UserDefaults.standard.dictionary(forKey: Self.savedPlacementsKey) as? [String: [String: Double]] ?? [:]
    }

    private func screenForCurrentFrame() -> NSScreen? {
        let center = CGPoint(x: frame.midX, y: frame.midY)
        if let containing = NSScreen.screens.first(where: { NSMouseInRect(center, $0.frame, false) }) {
            return containing
        }

        return screen ?? NSScreen.main
    }

    private func screenForDragMovement() -> NSScreen? {
        let pointer = NSEvent.mouseLocation
        if let underPointer = NSScreen.screens.first(where: { NSMouseInRect(pointer, $0.frame, false) }) {
            return underPointer
        }

        return nil
    }

    private func screenNumber(for screen: NSScreen) -> Int? {
        let key = NSDeviceDescriptionKey("NSScreenNumber")
        return (screen.deviceDescription[key] as? NSNumber)?.intValue
    }

    private func clampedOrigin(leftX: CGFloat, topY: CGFloat, in screen: NSScreen?) -> CGPoint {
        guard let screen else {
            return CGPoint(x: leftX, y: topY - frame.height)
        }

        let visibleFrame = screen.visibleFrame
        let maxX = visibleFrame.maxX - frame.width
        let minTopY = visibleFrame.minY + frame.height
        let maxTopY = visibleFrame.maxY
        let clampedX = min(max(leftX, visibleFrame.minX), maxX)
        let clampedTopY = min(max(topY, minTopY), maxTopY)
        return CGPoint(x: clampedX, y: clampedTopY - frame.height)
    }

    private func clamp(origin: CGPoint, in screen: NSScreen?) -> CGPoint {
        clampedOrigin(leftX: origin.x, topY: origin.y + frame.height, in: screen)
    }
}
