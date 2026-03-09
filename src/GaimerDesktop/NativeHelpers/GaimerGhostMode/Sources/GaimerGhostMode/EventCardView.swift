// EventCardView.swift
// Event notification cards for the ghost mode overlay — pure AppKit.
//
// Three variants:
//   1 = Voice:         Agent avatar + "is talking..." + animated dots
//   2 = Text:          Rounded card with title pill + text body
//   3 = TextWithImage: Text card + image below text
//
// Tap-to-dismiss on any card. Auto-dismiss handled by parent.
//
// PURE APPKIT — no SwiftUI.

import AppKit

// MARK: - EventCardNSView

class EventCardNSView: NSView {
    var onDismiss: (() -> Void)?

    private let variant: Int32
    private let cardBg = NSColor(red: 30/255, green: 30/255, blue: 46/255, alpha: 0.85) // #1E1E2E @ 0.85 — glass base
    private let accentColor = NSColor(red: 0x81/255, green: 0x8C/255, blue: 0xF8/255, alpha: 1) // #818CF8 — indigo-400

    // Indigo palette
    private let indigoStart = NSColor(red: 0x4F/255, green: 0x46/255, blue: 0xE5/255, alpha: 1)  // #4F46E5
    private let indigoMid = NSColor(red: 0x63/255, green: 0x66/255, blue: 0xF1/255, alpha: 1)    // #6366F1
    private let indigoEnd = NSColor(red: 0x81/255, green: 0x8C/255, blue: 0xF8/255, alpha: 1)    // #818CF8

    private var dismissed = false

    // Glass morphism layers
    private var blurView: NSVisualEffectView?
    private var borderLayer: CAGradientLayer?
    private var glowLayer: CAGradientLayer?

    init(variant: Int32, title: String?, text: String?, imagePath: String?, agentImage: NSImage?) {
        self.variant = variant
        super.init(frame: .zero)
        wantsLayer = true

        // Glass blur background
        let blur = NSVisualEffectView(frame: bounds)
        blur.blendingMode = .behindWindow
        blur.material = .hudWindow
        blur.state = .active
        blur.autoresizingMask = [.width, .height]
        blur.wantsLayer = true
        blur.layer?.cornerRadius = 12
        blur.layer?.masksToBounds = true
        addSubview(blur)
        self.blurView = blur

        layer?.cornerRadius = 12
        layer?.backgroundColor = cardBg.cgColor
        layer?.shadowColor = NSColor.black.cgColor
        layer?.shadowRadius = 6
        layer?.shadowOpacity = 0.3
        layer?.shadowOffset = NSSize(width: 0, height: -2)

        // Inner glow
        addInnerGlow()

        // Gradient border
        addGradientBorder()

        switch variant {
        case 1:
            buildVoiceCard(agentImage: agentImage)
        case 2:
            buildTextCard(title: title, text: text)
        case 3:
            buildTextWithImageCard(title: title, text: text, imagePath: imagePath)
        default:
            break
        }
    }

    required init?(coder: NSCoder) { fatalError() }

    override var isFlipped: Bool { true }

    override var intrinsicContentSize: NSSize {
        // Calculate height based on subviews
        var maxY: CGFloat = 0
        for sub in subviews {
            let bottom = sub.frame.origin.y + sub.frame.height
            if bottom > maxY { maxY = bottom }
        }
        return NSSize(width: 280, height: maxY + 14)
    }

    override func layout() {
        super.layout()
        blurView?.frame = bounds
        borderLayer?.frame = bounds
        glowLayer?.frame = bounds
        updateBorderMask()
    }

    private func safeDismiss() {
        guard !dismissed else { return }
        dismissed = true
        onDismiss?()
    }

    override func mouseUp(with event: NSEvent) {
        safeDismiss()
    }

    // MARK: - Glass Morphism Layers

    private func addGradientBorder() {
        let gradient = CAGradientLayer()
        gradient.frame = bounds
        gradient.colors = [indigoStart.cgColor, indigoMid.cgColor, indigoEnd.cgColor]
        gradient.startPoint = CGPoint(x: 0, y: 0)
        gradient.endPoint = CGPoint(x: 1, y: 1)
        gradient.cornerRadius = 12

        // Mask to show only border (even-odd fill rule)
        let maskLayer = CAShapeLayer()
        maskLayer.fillRule = .evenOdd
        let outerPath = CGPath(roundedRect: bounds, cornerWidth: 12, cornerHeight: 12, transform: nil)
        let innerRect = bounds.insetBy(dx: 1.5, dy: 1.5)
        let innerPath = CGPath(roundedRect: innerRect, cornerWidth: 10.5, cornerHeight: 10.5, transform: nil)
        let combined = CGMutablePath()
        combined.addPath(outerPath)
        combined.addPath(innerPath)
        maskLayer.path = combined
        gradient.mask = maskLayer

        layer?.addSublayer(gradient)
        self.borderLayer = gradient
    }

    private func updateBorderMask() {
        guard let borderLayer = borderLayer else { return }
        let maskLayer = CAShapeLayer()
        maskLayer.fillRule = .evenOdd
        let outerPath = CGPath(roundedRect: bounds, cornerWidth: 12, cornerHeight: 12, transform: nil)
        let innerRect = bounds.insetBy(dx: 1.5, dy: 1.5)
        let innerPath = CGPath(roundedRect: innerRect, cornerWidth: 10.5, cornerHeight: 10.5, transform: nil)
        let combined = CGMutablePath()
        combined.addPath(outerPath)
        combined.addPath(innerPath)
        maskLayer.path = combined
        borderLayer.mask = maskLayer
    }

    private func addInnerGlow() {
        let glow = CAGradientLayer()
        glow.frame = bounds
        glow.type = .radial
        glow.colors = [indigoEnd.withAlphaComponent(0.15).cgColor, NSColor.clear.cgColor]
        glow.startPoint = CGPoint(x: 0.5, y: 0)
        glow.endPoint = CGPoint(x: 1, y: 1)
        glow.cornerRadius = 12

        layer?.insertSublayer(glow, at: 0)
        self.glowLayer = glow
    }

    // MARK: - Voice Card (variant 1)

    private func buildVoiceCard(agentImage: NSImage?) {
        let padding: CGFloat = 14
        var x: CGFloat = padding
        let y: CGFloat = 10

        // Agent avatar
        let avatarSize: CGFloat = 36
        let avatarView = AvatarView(image: agentImage, size: avatarSize)
        avatarView.frame = NSRect(x: x, y: y, width: avatarSize, height: avatarSize)
        addSubview(avatarView)
        x += avatarSize + 10

        // "is talking" label
        let label = makeLabel(text: "is talking", fontSize: 12, weight: .medium, color: NSColor.white.withAlphaComponent(0.7))
        label.frame = NSRect(x: x, y: y + 2, width: 120, height: 16)
        addSubview(label)

        // Animated dots
        let dots = AnimatedDotsView()
        dots.frame = NSRect(x: x, y: y + 20, width: 40, height: 10)
        addSubview(dots)

        // Fixed height for voice card
        frame.size = NSSize(width: 280, height: y + avatarSize + 10)
    }

    // MARK: - Text Card (variant 2)

    private func buildTextCard(title: String?, text: String?) {
        let padding: CGFloat = 14
        let contentWidth: CGFloat = 280 - padding * 2
        var y: CGFloat = padding

        // Message type icon (SF Symbol: chat bubble)
        y = addMessageIcon("text.bubble.fill", at: y, padding: padding)

        // Divider below icon
        y = addDivider(at: y, padding: padding, width: contentWidth)

        if let title = title, !title.isEmpty {
            let pill = makeTitlePill(title)
            pill.frame.origin = NSPoint(x: padding, y: y)
            addSubview(pill)
            y += pill.frame.height + 8
        }

        if let text = text, !text.isEmpty {
            let label = makeLabel(text: text, fontSize: 15, weight: .bold, color: .white)
            label.lineBreakMode = .byWordWrapping
            label.maximumNumberOfLines = 6
            label.alignment = .center
            label.preferredMaxLayoutWidth = contentWidth
            let textSize = label.intrinsicContentSize
            label.frame = NSRect(x: padding, y: y, width: contentWidth, height: textSize.height)
            addSubview(label)
            y += textSize.height + 6
        }

        // Divider below message
        y = addDivider(at: y, padding: padding, width: contentWidth)

        // X dismiss icon centered below divider
        y = addDismissIcon(at: y, padding: padding, contentWidth: contentWidth)

        y += padding - 6
        frame.size = NSSize(width: 280, height: y)
    }

    // MARK: - Text + Image Card (variant 3)

    private func buildTextWithImageCard(title: String?, text: String?, imagePath: String?) {
        let padding: CGFloat = 14
        let contentWidth: CGFloat = 280 - padding * 2
        var y: CGFloat = padding

        // Message type icon (SF Symbol: photo)
        y = addMessageIcon("photo.on.rectangle", at: y, padding: padding)

        // Divider below icon
        y = addDivider(at: y, padding: padding, width: contentWidth)

        if let title = title, !title.isEmpty {
            let pill = makeTitlePill(title)
            pill.frame.origin = NSPoint(x: padding, y: y)
            addSubview(pill)
            y += pill.frame.height + 8
        }

        if let text = text, !text.isEmpty {
            let label = makeLabel(text: text, fontSize: 15, weight: .bold, color: .white)
            label.lineBreakMode = .byWordWrapping
            label.maximumNumberOfLines = 6
            label.alignment = .center
            label.preferredMaxLayoutWidth = contentWidth
            let textSize = label.intrinsicContentSize
            label.frame = NSRect(x: padding, y: y, width: contentWidth, height: textSize.height)
            addSubview(label)
            y += textSize.height + 6
        }

        // Divider below message
        y = addDivider(at: y, padding: padding, width: contentWidth)

        if let path = imagePath, let image = NSImage(contentsOfFile: path) {
            let maxW = contentWidth
            let aspect = image.size.width / max(image.size.height, 1)
            let imgH = min(maxW / aspect, 200)
            let imgView = NSImageView(frame: NSRect(x: padding, y: y, width: maxW, height: imgH))
            imgView.image = image
            imgView.imageScaling = .scaleProportionallyUpOrDown
            imgView.wantsLayer = true
            imgView.layer?.cornerRadius = 8
            imgView.layer?.masksToBounds = true
            imgView.layer?.borderWidth = 1
            imgView.layer?.borderColor = NSColor(red: 0x81/255, green: 0x8C/255, blue: 0xF8/255, alpha: 0.3).cgColor
            addSubview(imgView)
            y += imgH + 6
        }

        // Divider below image
        y = addDivider(at: y, padding: padding, width: contentWidth)

        // X dismiss icon centered below divider
        y = addDismissIcon(at: y, padding: padding, contentWidth: contentWidth)

        y += padding - 6
        frame.size = NSSize(width: 280, height: y)
    }

    // MARK: - Message Icon & Divider

    /// Adds a centered SF Symbol icon at the given y offset. Returns the new y after the icon.
    private func addMessageIcon(_ symbolName: String, at y: CGFloat, padding: CGFloat) -> CGFloat {
        let iconSize: CGFloat = 24
        let contentWidth: CGFloat = 280 - padding * 2

        if let sfImage = NSImage(systemSymbolName: symbolName, accessibilityDescription: nil) {
            let config = NSImage.SymbolConfiguration(pointSize: iconSize, weight: .medium)
            let tinted = sfImage.withSymbolConfiguration(config) ?? sfImage

            let iconView = NSImageView(frame: NSRect(
                x: padding + (contentWidth - iconSize) / 2,
                y: y,
                width: iconSize,
                height: iconSize
            ))
            iconView.image = tinted
            iconView.contentTintColor = accentColor
            iconView.imageScaling = .scaleProportionallyUpOrDown
            addSubview(iconView)
            return y + iconSize + 6
        }
        return y
    }

    /// Adds a centered X dismiss icon. Returns the new y after the icon.
    private func addDismissIcon(at y: CGFloat, padding: CGFloat, contentWidth: CGFloat) -> CGFloat {
        let iconSize: CGFloat = 16
        if let sfImage = NSImage(systemSymbolName: "xmark", accessibilityDescription: "Dismiss") {
            let config = NSImage.SymbolConfiguration(pointSize: iconSize, weight: .medium)
            let tinted = sfImage.withSymbolConfiguration(config) ?? sfImage

            let iconView = NSImageView(frame: NSRect(
                x: padding + (contentWidth - iconSize) / 2,
                y: y,
                width: iconSize,
                height: iconSize
            ))
            iconView.image = tinted
            iconView.contentTintColor = NSColor.white.withAlphaComponent(0.4)
            iconView.imageScaling = .scaleProportionallyUpOrDown
            addSubview(iconView)
            return y + iconSize + 4
        }
        return y
    }

    /// Adds a gradient horizontal divider. Returns the new y after the divider.
    private func addDivider(at y: CGFloat, padding: CGFloat, width: CGFloat) -> CGFloat {
        let divider = NSView(frame: NSRect(x: padding, y: y, width: width, height: 1))
        divider.wantsLayer = true
        let gradient = CAGradientLayer()
        gradient.frame = divider.bounds
        gradient.colors = [
            NSColor.clear.cgColor,
            NSColor.white.withAlphaComponent(0.3).cgColor,
            NSColor.clear.cgColor,
        ]
        gradient.startPoint = CGPoint(x: 0, y: 0.5)
        gradient.endPoint = CGPoint(x: 1, y: 0.5)
        divider.layer?.addSublayer(gradient)
        addSubview(divider)
        return y + 1 + 8
    }

    // MARK: - Helpers

    private func makeLabel(text: String, fontSize: CGFloat, weight: NSFont.Weight, color: NSColor) -> NSTextField {
        let label = NSTextField(labelWithString: text)
        label.font = NSFont.systemFont(ofSize: fontSize, weight: weight)
        label.textColor = color
        label.backgroundColor = .clear
        label.isBezeled = false
        label.isEditable = false
        label.isSelectable = false
        label.cell?.wraps = true
        return label
    }

    private func makeTitlePill(_ title: String) -> NSView {
        let label = makeLabel(text: title, fontSize: 11, weight: .bold, color: .white)  // white text on glass
        let textSize = label.intrinsicContentSize
        let pillW = textSize.width + 20
        let pillH = textSize.height + 8

        let pill = NSView(frame: NSRect(x: 0, y: 0, width: pillW, height: pillH))
        pill.wantsLayer = true
        pill.layer?.backgroundColor = NSColor(red: 0x81/255, green: 0x8C/255, blue: 0xF8/255, alpha: 0.15).cgColor  // glass
        pill.layer?.cornerRadius = pillH / 2
        pill.layer?.borderWidth = 1
        pill.layer?.borderColor = NSColor(red: 0x81/255, green: 0x8C/255, blue: 0xF8/255, alpha: 0.3).cgColor  // indigo-400/30

        label.frame = NSRect(x: 10, y: 4, width: textSize.width, height: textSize.height)
        pill.addSubview(label)

        return pill
    }
}

// MARK: - AvatarView

private class AvatarView: NSView {
    init(image: NSImage?, size: CGFloat) {
        super.init(frame: NSRect(x: 0, y: 0, width: size, height: size))
        wantsLayer = true
        layer?.cornerRadius = size / 2
        layer?.masksToBounds = true

        if let img = image {
            layer?.contents = img
            layer?.contentsGravity = .resizeAspectFill
        } else {
            layer?.backgroundColor = NSColor.gray.withAlphaComponent(0.5).cgColor
            // Simple controller icon as fallback
            let iconLayer = CAShapeLayer()
            let path = CGMutablePath()
            let cx = size / 2, cy = size / 2, s = size * 0.2
            path.addRoundedRect(in: NSRect(x: cx - s, y: cy - s * 0.5, width: s * 2, height: s), cornerWidth: 2, cornerHeight: 2)
            iconLayer.path = path
            iconLayer.fillColor = NSColor.white.cgColor
            layer?.addSublayer(iconLayer)
        }
    }

    required init?(coder: NSCoder) { fatalError() }
}

// MARK: - AnimatedDotsView

/// Simple animated dots indicator ("..." with pulsing).
/// Uses DispatchSourceTimer — automatically cancelled on removal from window.
class AnimatedDotsView: NSView {
    private var dotLayers: [CAShapeLayer] = []
    private var timer: DispatchSourceTimer?
    private var phase: Int = 0

    override init(frame: NSRect) {
        super.init(frame: frame)
        wantsLayer = true

        for i in 0..<3 {
            let dot = CAShapeLayer()
            let x = CGFloat(i) * 9
            dot.path = CGPath(ellipseIn: NSRect(x: x, y: 2, width: 5, height: 5), transform: nil)
            dot.fillColor = NSColor.white.cgColor
            dot.opacity = 0.3
            layer?.addSublayer(dot)
            dotLayers.append(dot)
        }
    }

    required init?(coder: NSCoder) { fatalError() }

    override func viewDidMoveToWindow() {
        super.viewDidMoveToWindow()
        if window != nil {
            startAnimation()
        } else {
            stopAnimation()
        }
    }

    private func startAnimation() {
        guard timer == nil else { return }
        let t = DispatchSource.makeTimerSource(queue: .main)
        t.schedule(deadline: .now(), repeating: 0.4)
        t.setEventHandler { [weak self] in
            guard let self = self else { return }
            self.phase = (self.phase + 1) % 3
            for (i, dot) in self.dotLayers.enumerated() {
                dot.opacity = (i == self.phase) ? 1.0 : 0.3
            }
        }
        t.resume()
        timer = t
    }

    private func stopAnimation() {
        timer?.cancel()
        timer = nil
    }
}
