// GearIconView.swift
// 24pt circular gear icon for ghost mode overlay — pure AppKit.
//
// Positioned to the left of the FAB. Tapping opens the audio control card.
// Same hit-test and press-feedback pattern as FabButtonView.
//
// PURE APPKIT — no SwiftUI.

import AppKit

class GearIconView: NSView {
    var onTap: (() -> Void)?

    private let darkBg = NSColor(red: 30/255, green: 30/255, blue: 46/255, alpha: 0.7)
    private let iconSize: CGFloat = 24

    private let backgroundLayer = CAShapeLayer()
    private let iconImageView: NSImageView

    init() {
        iconImageView = NSImageView(frame: NSRect(x: 5, y: 5, width: 14, height: 14))
        super.init(frame: NSRect(x: 0, y: 0, width: 24, height: 24))
        setupLayers()
    }

    required init?(coder: NSCoder) { fatalError() }

    override var isFlipped: Bool { false }

    private func setupLayers() {
        wantsLayer = true
        layer?.masksToBounds = false

        // Circular background
        let circlePath = CGPath(ellipseIn: NSRect(x: 0, y: 0, width: iconSize, height: iconSize), transform: nil)
        backgroundLayer.path = circlePath
        backgroundLayer.fillColor = darkBg.cgColor
        layer?.addSublayer(backgroundLayer)

        // SF Symbol gear icon
        if let sfImage = NSImage(systemSymbolName: "gearshape.fill", accessibilityDescription: nil) {
            let config = NSImage.SymbolConfiguration(pointSize: 14, weight: .medium)
            iconImageView.image = sfImage.withSymbolConfiguration(config) ?? sfImage
            iconImageView.contentTintColor = NSColor.white.withAlphaComponent(0.8)
            iconImageView.imageScaling = .scaleProportionallyUpOrDown
        }
        addSubview(iconImageView)
    }

    // MARK: - Mouse Handling

    override func mouseDown(with event: NSEvent) {
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.1
            self.animator().alphaValue = 0.7
        }
    }

    override func mouseUp(with event: NSEvent) {
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.1
            self.animator().alphaValue = 1.0
        }
        let location = convert(event.locationInWindow, from: nil)
        if bounds.contains(location) {
            onTap?()
        }
    }

    // MARK: - Circular Hit Test

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
