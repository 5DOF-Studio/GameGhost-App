// FabView.swift
// Floating Action Button for the ghost mode overlay — pure AppKit.
//
// 56pt circular button with three visual states:
//   Off-Game (not connected): Yellow background + "GHOST MODE" black text
//   In-Game  (connected):     Agent portrait + yellow glow ring
//   Active   (ghost mode on): Agent portrait + yellow glow ring (same as in-game)
//
// PURE APPKIT — no SwiftUI.

import AppKit

// MARK: - FabButtonView

class FabButtonView: NSView {
    var onTap: (() -> Void)?
    var onDrag: ((NSPoint) -> Void)?

    private var dragStart: NSPoint?
    private var isDragging = false

    private let darkBg = NSColor(red: 30/255, green: 30/255, blue: 46/255, alpha: 1) // #1E1E2E
    private let glowYellow = NSColor(red: 1.0, green: 0.84, blue: 0.0, alpha: 1)

    private var agentImage: NSImage?
    private var isActive: Bool = false
    private var isConnected: Bool = false

    private let backgroundLayer = CAShapeLayer()
    private let imageLayer = CALayer()
    private let iconLayer = CAShapeLayer()
    private let glowRingLayer = CAShapeLayer()
    private let labelLayer = CATextLayer()

    init(agentImage: NSImage?, isActive: Bool, isConnected: Bool) {
        self.agentImage = agentImage
        self.isActive = isActive
        self.isConnected = isConnected
        super.init(frame: NSRect(x: 0, y: 0, width: 56, height: 56))
        setupLayers()
    }

    required init?(coder: NSCoder) { fatalError() }

    override var isFlipped: Bool { false }

    private func setupLayers() {
        wantsLayer = true
        layer?.masksToBounds = false

        let size: CGFloat = 56
        let circularPath = CGPath(ellipseIn: NSRect(x: 0, y: 0, width: size, height: size), transform: nil)

        // Background circle
        backgroundLayer.path = circularPath
        backgroundLayer.fillColor = darkBg.cgColor
        layer?.addSublayer(backgroundLayer)

        // Agent image (clipped to circle)
        let imageInset: CGFloat = 4
        let imageSize = size - imageInset * 2
        imageLayer.frame = NSRect(x: imageInset, y: imageInset, width: imageSize, height: imageSize)
        imageLayer.cornerRadius = imageSize / 2
        imageLayer.masksToBounds = true
        imageLayer.contentsGravity = .resizeAspectFill
        layer?.addSublayer(imageLayer)

        // Fallback icon (shown when no agent image and connected)
        iconLayer.frame = NSRect(x: 0, y: 0, width: size, height: size)
        iconLayer.isHidden = true
        layer?.addSublayer(iconLayer)

        // Glow ring
        glowRingLayer.path = circularPath
        glowRingLayer.fillColor = nil
        glowRingLayer.strokeColor = glowYellow.cgColor
        glowRingLayer.lineWidth = 2
        layer?.addSublayer(glowRingLayer)

        // "GHOST MODE" label (shown when not connected)
        let textHeight: CGFloat = 28
        labelLayer.string = "GHOST\nMODE"
        labelLayer.fontSize = 10
        labelLayer.font = NSFont.systemFont(ofSize: 10, weight: .heavy) as CTFont
        labelLayer.foregroundColor = NSColor.black.cgColor
        labelLayer.alignmentMode = .center
        labelLayer.contentsScale = NSScreen.main?.backingScaleFactor ?? 2.0
        labelLayer.isWrapped = true
        labelLayer.truncationMode = .none
        labelLayer.frame = NSRect(x: 2, y: (size - textHeight) / 2, width: size - 4, height: textHeight)
        labelLayer.isHidden = true
        layer?.addSublayer(labelLayer)

        applyState()
    }

    func update(agentImage: NSImage?, isActive: Bool, isConnected: Bool) {
        self.agentImage = agentImage
        self.isActive = isActive
        self.isConnected = isConnected
        applyState()
    }

    private func applyState() {
        if !isConnected {
            // Off-game: yellow circle + "GHOST MODE" black text
            backgroundLayer.fillColor = glowYellow.cgColor
            imageLayer.isHidden = true
            iconLayer.isHidden = true
            labelLayer.isHidden = false
            glowRingLayer.isHidden = true
            glowRingLayer.shadowOpacity = 0
            alphaValue = 1.0
        } else {
            // In-game (connected) or active: dark bg + portrait + glow ring
            backgroundLayer.fillColor = darkBg.cgColor
            labelLayer.isHidden = true

            if let img = agentImage {
                imageLayer.isHidden = false
                imageLayer.contents = img
                iconLayer.isHidden = true
            } else {
                imageLayer.isHidden = true
                iconLayer.isHidden = false
                drawControllerIcon()
            }

            // Glow ring always visible when connected
            glowRingLayer.isHidden = false
            glowRingLayer.shadowColor = glowYellow.cgColor
            glowRingLayer.shadowRadius = 12
            glowRingLayer.shadowOpacity = 0.6
            glowRingLayer.shadowOffset = .zero

            alphaValue = 1.0
        }
    }

    private func drawControllerIcon() {
        // Simple gamecontroller glyph — a rounded rect with two bumps
        let size: CGFloat = 56
        let cx = size / 2
        let cy = size / 2
        let path = CGMutablePath()

        // Body
        path.addRoundedRect(in: NSRect(x: cx - 10, y: cy - 6, width: 20, height: 12), cornerWidth: 4, cornerHeight: 4)
        // Left grip
        path.addRoundedRect(in: NSRect(x: cx - 14, y: cy - 3, width: 8, height: 6), cornerWidth: 2, cornerHeight: 2)
        // Right grip
        path.addRoundedRect(in: NSRect(x: cx + 6, y: cy - 3, width: 8, height: 6), cornerWidth: 2, cornerHeight: 2)

        iconLayer.path = path
        iconLayer.strokeColor = nil
        iconLayer.fillColor = NSColor.white.cgColor
    }

    // MARK: - Mouse Handling (tap + drag)

    override func mouseDown(with event: NSEvent) {
        dragStart = event.locationInWindow
        isDragging = false
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.1
            self.animator().alphaValue = 0.7
        }
    }

    override func mouseDragged(with event: NSEvent) {
        guard let start = dragStart else { return }
        let current = event.locationInWindow
        let dx = current.x - start.x
        let dy = current.y - start.y
        // Threshold: 4pt before we consider it a drag
        if !isDragging && (dx * dx + dy * dy) > 16 {
            isDragging = true
        }
        if isDragging {
            // Report the new center position in superview coordinates
            let newCenter = NSPoint(
                x: frame.midX + (current.x - start.x),
                y: frame.midY + (current.y - start.y)
            )
            dragStart = current
            onDrag?(newCenter)
        }
    }

    override func mouseUp(with event: NSEvent) {
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.1
            self.animator().alphaValue = 1.0
        }
        if !isDragging {
            let location = convert(event.locationInWindow, from: nil)
            if bounds.contains(location) {
                onTap?()
            }
        }
        dragStart = nil
        isDragging = false
    }

    override func hitTest(_ point: NSPoint) -> NSView? {
        let local = convert(point, from: superview)
        let center = NSPoint(x: bounds.midX, y: bounds.midY)
        let dx = local.x - center.x
        let dy = local.y - center.y
        let radius = bounds.width / 2
        if dx * dx + dy * dy <= radius * radius {
            return self
        }
        return nil
    }
}
