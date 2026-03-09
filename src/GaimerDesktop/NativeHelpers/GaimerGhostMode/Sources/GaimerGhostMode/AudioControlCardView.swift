// AudioControlCardView.swift
// Compact audio control card for ghost mode overlay — pure AppKit.
//
// Shows 3 toggle switches (MIC, AUTO, AI-MIC) with LED indicators.
// Same visual style as EventCardNSView: translucent dark bg, rounded corners, shadow.
// Tap background to dismiss. Auto-dismiss handled by parent.
//
// PURE APPKIT — no SwiftUI.

import AppKit

// MARK: - NativeToggleSwitch

/// Compact AppKit toggle switch with metallic handle and LED bar.
class NativeToggleSwitch: NSView {
    var isOn: Bool = false {
        didSet { animateToState(isOn) }
    }
    var onToggleChanged: ((Bool) -> Void)?

    private let trackWidth: CGFloat = 48
    private let trackHeight: CGFloat = 26
    private let handleWidth: CGFloat = 20
    private let handleHeight: CGFloat = 22
    private let ledWidth: CGFloat = 16
    private let ledHeight: CGFloat = 4

    private let trackLayer = CALayer()
    private let handleLayer = CALayer()
    private let handleGradient = CAGradientLayer()
    private let ledLayer = CALayer()

    private let offTrackColor = NSColor(red: 40/255, green: 40/255, blue: 56/255, alpha: 1).cgColor
    private let onTrackColor = NSColor(red: 50/255, green: 50/255, blue: 70/255, alpha: 1).cgColor

    var ledColor: NSColor = NSColor(red: 0.133, green: 0.773, blue: 0.369, alpha: 1) { // #22c55e
        didSet { updateLED() }
    }

    init(ledColor: NSColor) {
        self.ledColor = ledColor
        super.init(frame: NSRect(x: 0, y: 0, width: 48, height: 34))
        setupLayers()
    }

    required init?(coder: NSCoder) { fatalError() }

    override var isFlipped: Bool { true }

    private func setupLayers() {
        wantsLayer = true

        // LED bar (above track)
        ledLayer.frame = NSRect(x: (trackWidth - ledWidth) / 2, y: 0, width: ledWidth, height: ledHeight)
        ledLayer.cornerRadius = ledHeight / 2
        ledLayer.backgroundColor = NSColor.white.withAlphaComponent(0.15).cgColor
        layer?.addSublayer(ledLayer)

        // Track
        let trackY = ledHeight + 4
        trackLayer.frame = NSRect(x: 0, y: trackY, width: trackWidth, height: trackHeight)
        trackLayer.cornerRadius = trackHeight / 2
        trackLayer.backgroundColor = offTrackColor
        trackLayer.borderColor = NSColor.white.withAlphaComponent(0.08).cgColor
        trackLayer.borderWidth = 1
        layer?.addSublayer(trackLayer)

        // Handle with metallic gradient
        let handleX: CGFloat = 2
        let handleY = trackY + (trackHeight - handleHeight) / 2
        handleLayer.frame = NSRect(x: handleX, y: handleY, width: handleWidth, height: handleHeight)
        handleLayer.cornerRadius = handleHeight / 2

        handleGradient.frame = handleLayer.bounds
        handleGradient.cornerRadius = handleHeight / 2
        handleGradient.colors = [
            NSColor(white: 0.85, alpha: 1).cgColor,
            NSColor(white: 0.6, alpha: 1).cgColor
        ]
        handleGradient.startPoint = CGPoint(x: 0.5, y: 0)
        handleGradient.endPoint = CGPoint(x: 0.5, y: 1)
        handleLayer.addSublayer(handleGradient)

        handleLayer.shadowColor = NSColor.black.cgColor
        handleLayer.shadowRadius = 2
        handleLayer.shadowOpacity = 0.3
        handleLayer.shadowOffset = CGSize(width: 0, height: 1)

        layer?.addSublayer(handleLayer)
    }

    private func animateToState(_ on: Bool) {
        let ledY = ledLayer.frame.origin.y
        let trackY = trackLayer.frame.origin.y
        let handleY = trackY + (trackHeight - handleHeight) / 2
        let handleX: CGFloat = on ? (trackWidth - handleWidth - 2) : 2

        CATransaction.begin()
        CATransaction.setAnimationDuration(0.25)
        CATransaction.setAnimationTimingFunction(CAMediaTimingFunction(name: .easeInEaseOut))

        handleLayer.frame = NSRect(x: handleX, y: handleY, width: handleWidth, height: handleHeight)
        handleGradient.frame = handleLayer.bounds

        trackLayer.backgroundColor = on ? onTrackColor : offTrackColor

        updateLED()

        CATransaction.commit()
    }

    private func updateLED() {
        if isOn {
            ledLayer.backgroundColor = ledColor.cgColor
            ledLayer.shadowColor = ledColor.cgColor
            ledLayer.shadowRadius = 4
            ledLayer.shadowOpacity = 0.8
            ledLayer.shadowOffset = .zero
        } else {
            ledLayer.backgroundColor = NSColor.white.withAlphaComponent(0.15).cgColor
            ledLayer.shadowOpacity = 0
        }
    }

    override func mouseUp(with event: NSEvent) {
        let location = convert(event.locationInWindow, from: nil)
        if bounds.contains(location) {
            isOn.toggle()
            onToggleChanged?(isOn)
        }
    }

    override func hitTest(_ point: NSPoint) -> NSView? {
        let local = convert(point, from: superview)
        if bounds.contains(local) {
            return self
        }
        return nil
    }
}

// MARK: - AudioControlCardView

class AudioControlCardView: NSView {
    var onToggleChanged: ((Int32, Bool) -> Void)?
    var onDismiss: (() -> Void)?

    private let cardBg = NSColor(red: 30/255, green: 30/255, blue: 46/255, alpha: 0.55)
    private let cyanAccent = NSColor(red: 0, green: 0.9, blue: 1.0, alpha: 1)

    private var dismissed = false
    private var toggleSwitches: [NativeToggleSwitch] = []

    init(micActive: Bool, commentaryActive: Bool, aiMicActive: Bool) {
        super.init(frame: NSRect(x: 0, y: 0, width: 200, height: 130))
        wantsLayer = true
        layer?.cornerRadius = 12
        layer?.backgroundColor = cardBg.cgColor
        layer?.shadowColor = NSColor.black.cgColor
        layer?.shadowRadius = 6
        layer?.shadowOpacity = 0.3
        layer?.shadowOffset = NSSize(width: 0, height: -2)

        buildCard(micActive: micActive, commentaryActive: commentaryActive, aiMicActive: aiMicActive)
    }

    required init?(coder: NSCoder) { fatalError() }

    override var isFlipped: Bool { true }

    override var intrinsicContentSize: NSSize {
        return NSSize(width: 200, height: 130)
    }

    private func buildCard(micActive: Bool, commentaryActive: Bool, aiMicActive: Bool) {
        let padding: CGFloat = 14
        let contentWidth: CGFloat = 200 - padding * 2
        var y: CGFloat = padding

        // Speaker icon (centered)
        let iconSize: CGFloat = 20
        if let sfImage = NSImage(systemSymbolName: "speaker.wave.2.fill", accessibilityDescription: nil) {
            let config = NSImage.SymbolConfiguration(pointSize: iconSize, weight: .medium)
            let iconView = NSImageView(frame: NSRect(
                x: padding + (contentWidth - iconSize) / 2,
                y: y,
                width: iconSize,
                height: iconSize
            ))
            iconView.image = sfImage.withSymbolConfiguration(config) ?? sfImage
            iconView.contentTintColor = cyanAccent
            iconView.imageScaling = .scaleProportionallyUpOrDown
            addSubview(iconView)
        }
        y += iconSize + 6

        // Top divider
        let divider1 = NSView(frame: NSRect(x: padding, y: y, width: contentWidth, height: 1))
        divider1.wantsLayer = true
        divider1.layer?.backgroundColor = NSColor.white.withAlphaComponent(0.15).cgColor
        addSubview(divider1)
        y += 1 + 8

        // 3 toggle switches arranged horizontally
        let toggleWidth: CGFloat = 48
        let toggleHeight: CGFloat = 34
        let spacing: CGFloat = 14
        let totalTogglesWidth = toggleWidth * 3 + spacing * 2
        var tx = padding + (contentWidth - totalTogglesWidth) / 2

        let ledColors: [NSColor] = [
            NSColor(red: 0.133, green: 0.773, blue: 0.369, alpha: 1), // #22c55e green
            NSColor(red: 0.949, green: 0.663, blue: 0.0, alpha: 1),   // #f2a900 gold
            NSColor(red: 0.306, green: 0.639, blue: 1.0, alpha: 1)    // #4ea3ff blue
        ]
        let labels = ["MIC", "AUTO", "AI-MIC"]
        let states = [micActive, commentaryActive, aiMicActive]

        for i in 0..<3 {
            let toggle = NativeToggleSwitch(ledColor: ledColors[i])
            toggle.frame = NSRect(x: tx, y: y, width: toggleWidth, height: toggleHeight)
            toggle.isOn = states[i]
            let index = Int32(i)
            toggle.onToggleChanged = { [weak self] newValue in
                self?.onToggleChanged?(index, newValue)
            }
            addSubview(toggle)
            toggleSwitches.append(toggle)

            // Label below toggle
            let label = NSTextField(labelWithString: labels[i])
            label.font = NSFont.systemFont(ofSize: 9, weight: .bold)
            label.textColor = NSColor.white.withAlphaComponent(0.6)
            label.backgroundColor = .clear
            label.isBezeled = false
            label.isEditable = false
            label.isSelectable = false
            label.alignment = .center
            label.frame = NSRect(x: tx, y: y + toggleHeight + 2, width: toggleWidth, height: 12)
            addSubview(label)

            tx += toggleWidth + spacing
        }
        y += toggleHeight + 12 + 6

        // Bottom divider
        let divider2 = NSView(frame: NSRect(x: padding, y: y, width: contentWidth, height: 1))
        divider2.wantsLayer = true
        divider2.layer?.backgroundColor = NSColor.white.withAlphaComponent(0.15).cgColor
        addSubview(divider2)
    }

    func updateToggleStates(micActive: Bool, commentaryActive: Bool, aiMicActive: Bool) {
        let states = [micActive, commentaryActive, aiMicActive]
        for (i, toggle) in toggleSwitches.enumerated() where i < states.count {
            if toggle.isOn != states[i] {
                toggle.isOn = states[i]
            }
        }
    }

    private func safeDismiss() {
        guard !dismissed else { return }
        dismissed = true
        onDismiss?()
    }

    override func mouseUp(with event: NSEvent) {
        // Only dismiss if the tap was on the card background (not a toggle)
        let location = convert(event.locationInWindow, from: nil)
        for toggle in toggleSwitches {
            let toggleLocal = toggle.convert(location, from: self)
            if toggle.bounds.contains(toggleLocal) {
                return // Toggle handles its own tap
            }
        }
        safeDismiss()
    }
}
