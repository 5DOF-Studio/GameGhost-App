//
//  CodexMetalBadge.swift
//  GhostFab-AppKit
//
//  Reusable circular metallic badge NSView component.
//  Renders: gradient background + highlight sheen + border stroke + centered glyph + toggleable glow.
//  Source of truth: DesignReference/GhostFabCodex/UI/Controls/CodexMetalBadge.xaml(.cs)
//

import AppKit
import QuartzCore

/// Circular metallic badge view used for audio toggle buttons and status indicators.
/// Renders a layered metallic circle with gradient background, highlight sheen, stroke border,
/// centered bold glyph, and optional two-layer glow effect behind the glyph.
///
/// Layer hierarchy (bottom to top, matching MAUI CodexMetalBadge.xaml):
/// 1. Background gradient (CAGradientLayer) -- fills circle
/// 2. Highlight gradient (CAGradientLayer) -- top-lit metallic sheen at 0.45 opacity
/// 3. Glow glyph 1 (CATextLayer) -- behind main glyph, 0.3 opacity when showGlow=true
/// 4. Glow glyph 2 (CATextLayer) -- behind main glyph, 0.6 opacity when showGlow=true
/// 5. Main glyph (CATextLayer) -- centered bold text
///
/// Frame-based layout -- parent sets this view's frame directly.
public class CodexMetalBadge: NSView {

    // MARK: - Configurable Properties

    /// Badge diameter in points. Default: SmallBadgeSize (36pt).
    public var diameter: CGFloat {
        didSet { applyState() }
    }

    /// Factory closure that creates the background gradient layer.
    /// Called each time `applyState()` rebuilds layers (never cached).
    public var backgroundGradientFactory: () -> CAGradientLayer = GhostFabTokens.makeSteelRingGradient {
        didSet { applyState() }
    }

    /// Factory closure that creates the highlight overlay gradient layer.
    /// Called each time `applyState()` rebuilds layers (never cached).
    public var highlightGradientFactory: () -> CAGradientLayer = GhostFabTokens.makeBadgeHighlightGradient {
        didSet { applyState() }
    }

    /// Stroke border color. Default: white at 15% opacity (BadgeStrokeBrush equivalent).
    public var strokeColor: NSColor = NSColor(srgbRed: 1.0, green: 1.0, blue: 1.0, alpha: 0.15) {
        didSet { applyState() }
    }

    /// Glyph text displayed in the badge center.
    public var glyphText: String = "" {
        didSet { applyState() }
    }

    /// Color of the main glyph text.
    public var glyphColor: NSColor = .white {
        didSet { applyState() }
    }

    /// Color of the glow text layers behind the main glyph.
    public var glowColor: NSColor = .white {
        didSet { applyState() }
    }

    /// Whether the two glow layers behind the glyph are visible.
    /// When true: glow1 opacity = 0.3, glow2 opacity = 0.6 (matching MAUI).
    /// When false: both glow layers opacity = 0.
    public var showGlow: Bool = false {
        didSet { applyState() }
    }

    /// Font size for all glyph text layers (main + glow).
    public var glyphSize: CGFloat = 16 {
        didSet { applyState() }
    }

    /// Optional icon image displayed instead of text glyph.
    /// Rendered as template image, tinted with glyphColor.
    public var iconImage: NSImage? {
        didSet { applyState() }
    }

    // MARK: - Layer References

    private var backgroundLayer: CAGradientLayer?
    private var highlightLayer: CAGradientLayer?
    private var glowGlyph1: CATextLayer?
    private var glowGlyph2: CATextLayer?
    private var mainGlyph: CATextLayer?
    private var iconLayer: CALayer?

    // MARK: - Initialization

    public init(diameter: CGFloat = GhostFabTokens.SmallBadgeSize) {
        self.diameter = diameter
        super.init(frame: NSRect(x: 0, y: 0, width: diameter, height: diameter))
        wantsLayer = true
        setupLayers()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    // MARK: - Intrinsic Size

    override public var intrinsicContentSize: NSSize {
        return NSSize(width: diameter, height: diameter)
    }

    // MARK: - Layer Setup

    private func setupLayers() {
        guard let layer = self.layer else { return }

        // Circular clipping
        layer.cornerRadius = diameter / 2
        layer.masksToBounds = true

        // Border stroke
        layer.borderWidth = 1.0
        layer.borderColor = strokeColor.cgColor

        // Build all sublayers
        applyState()
    }

    // MARK: - State Application

    /// Rebuilds all layers to reflect current property values.
    /// Mirrors MAUI CodexMetalBadge.ApplyState().
    public func applyState() {
        guard let layer = self.layer else { return }

        let d = max(diameter, 1)

        // Update root layer geometry
        layer.cornerRadius = d / 2
        layer.borderColor = strokeColor.cgColor

        // Remove old gradient sublayers
        backgroundLayer?.removeFromSuperlayer()
        highlightLayer?.removeFromSuperlayer()

        // 1. Background gradient (fills circle)
        let bg = backgroundGradientFactory()
        bg.frame = bounds
        bg.cornerRadius = d / 2
        bg.masksToBounds = true
        layer.addSublayer(bg)
        backgroundLayer = bg

        // 2. Highlight gradient (top-lit metallic sheen)
        let hl = highlightGradientFactory()
        hl.frame = bounds
        hl.cornerRadius = d / 2
        hl.masksToBounds = true
        hl.opacity = 0.45
        layer.addSublayer(hl)
        highlightLayer = hl

        let hasIcon = iconImage != nil

        // 3. Glow glyph 1 (behind main, lower opacity)
        if glowGlyph1 == nil {
            glowGlyph1 = makeTextLayer(color: glowColor)
            layer.addSublayer(glowGlyph1!)
        }
        configureTextLayer(glowGlyph1!, color: glowColor)
        glowGlyph1!.opacity = hasIcon ? 0 : (showGlow ? 0.3 : 0)

        // 4. Glow glyph 2 (behind main, higher opacity)
        if glowGlyph2 == nil {
            glowGlyph2 = makeTextLayer(color: glowColor)
            layer.addSublayer(glowGlyph2!)
        }
        configureTextLayer(glowGlyph2!, color: glowColor)
        glowGlyph2!.opacity = hasIcon ? 0 : (showGlow ? 0.6 : 0)

        // 5. Main glyph (top layer)
        if mainGlyph == nil {
            mainGlyph = makeTextLayer(color: glyphColor)
            layer.addSublayer(mainGlyph!)
        }
        configureTextLayer(mainGlyph!, color: glyphColor)
        mainGlyph!.opacity = hasIcon ? 0 : 1.0

        // 6. Icon image (replaces text glyph when set)
        if let icon = iconImage {
            if iconLayer == nil {
                iconLayer = CALayer()
                iconLayer!.contentsGravity = .resizeAspect
            }
            // Always re-add on top so gradients don't bury it
            iconLayer!.removeFromSuperlayer()
            layer.addSublayer(iconLayer!)
            // Icons are pre-colored assets — render directly without tinting
            var iconRect = NSRect(origin: .zero, size: icon.size)
            iconLayer!.contents = icon.cgImage(forProposedRect: &iconRect, context: nil, hints: nil)
            iconLayer!.contentsScale = NSScreen.main?.backingScaleFactor ?? 2.0
            iconLayer!.isHidden = false
            let iconInset = d * 0.22
            iconLayer!.frame = NSRect(x: iconInset, y: iconInset,
                                      width: d - iconInset * 2, height: d - iconInset * 2)
        } else {
            iconLayer?.isHidden = true
        }

        invalidateIntrinsicContentSize()
        needsLayout = true
    }

    // MARK: - Text Layer Helpers

    private func makeTextLayer(color: NSColor) -> CATextLayer {
        let textLayer = CATextLayer()
        textLayer.string = glyphText
        textLayer.fontSize = glyphSize
        textLayer.font = NSFont.boldSystemFont(ofSize: glyphSize)
        textLayer.foregroundColor = color.cgColor
        textLayer.alignmentMode = .center
        textLayer.contentsScale = NSScreen.main?.backingScaleFactor ?? 2.0
        textLayer.isWrapped = false
        textLayer.truncationMode = .none
        return textLayer
    }

    private func configureTextLayer(_ textLayer: CATextLayer, color: NSColor) {
        textLayer.string = glyphText
        textLayer.fontSize = glyphSize
        textLayer.font = NSFont.boldSystemFont(ofSize: glyphSize)
        textLayer.foregroundColor = color.cgColor
        textLayer.contentsScale = NSScreen.main?.backingScaleFactor ?? 2.0
    }

    /// Renders an icon image tinted with the given color as a CGImage.
    /// Uses sourceAtop compositing so only opaque pixels are tinted.
    private static func tintedCGImage(from image: NSImage, color: NSColor) -> CGImage? {
        let size = NSSize(width: 64, height: 64)
        let tinted = NSImage(size: size, flipped: false) { rect in
            image.draw(in: rect, from: .zero, operation: .sourceOver, fraction: 1.0)
            color.set()
            rect.fill(using: .sourceAtop)
            return true
        }
        var r = NSRect(origin: .zero, size: size)
        return tinted.cgImage(forProposedRect: &r, context: nil, hints: nil)
    }

    // MARK: - Layout

    override public func layout() {
        super.layout()

        let layerBounds = bounds

        backgroundLayer?.frame = layerBounds
        highlightLayer?.frame = layerBounds

        // Center text layers vertically and horizontally within the badge.
        // CATextLayer needs explicit frame sizing for proper centering.
        let font = NSFont.boldSystemFont(ofSize: glyphSize)
        let textHeight = ceil(font.ascender - font.descender + font.leading)
        let textY = (layerBounds.height - textHeight) / 2
        let textFrame = NSRect(x: 0, y: textY, width: layerBounds.width, height: textHeight)

        glowGlyph1?.frame = textFrame
        glowGlyph2?.frame = textFrame
        mainGlyph?.frame = textFrame

        // Icon centered with ~55% of badge diameter
        if let il = iconLayer, !il.isHidden {
            let iconInset = diameter * 0.22
            il.frame = NSRect(x: iconInset, y: iconInset,
                              width: layerBounds.width - iconInset * 2,
                              height: layerBounds.height - iconInset * 2)
        }
    }
}
