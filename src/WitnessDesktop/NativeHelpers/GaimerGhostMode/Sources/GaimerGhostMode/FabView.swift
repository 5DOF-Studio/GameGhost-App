//
//  FabView.swift
//  GhostFab-AppKit
//
//  Floating Action Button visual assembly: ring, core, portrait, and shadow.
//  The FAB is the visual anchor of the ghost panel, displaying the agent's
//  portrait image and indicating connection state through its ring gradient.
//  Source of truth: DesignReference/GhostFabCodex/UI/Views/CodexRendererView.xaml
//

import AppKit
import QuartzCore

/// C-compatible void callback for FAB tap events.
/// Matches the @convention(c) signature used by @_cdecl exports.
public typealias VoidCallback = @convention(c) () -> Void

/// Renders the complete FAB circle assembly: shadow -> ring -> core -> portrait.
/// Frame-based layout -- parent (GhostContentView) sets this view's frame directly.
public class FabView: NSView {

    // MARK: - Subviews

    private var ghostShadow: NSView!
    private var fabRing: NSView!
    private var fabCore: NSView!
    private var fabPortraitLayer: CALayer!
    private var backlitGlow: CALayer!

    // MARK: - Gradient Layers (stored for layout updates)

    private var shadowGradient: CAGradientLayer!
    private var ringGradient: CAGradientLayer!
    private var coreGradient: CAGradientLayer!

    // MARK: - State

    /// Stored callback fired when FAB is tapped. Set via ghost_panel_set_fab_tap_callback.
    private var tapCallback: VoidCallback?

    /// Whether the FAB is in active state (agent is processing/speaking).
    private var isActive: Bool = false

    /// Whether the FAB is connected to the agent service.
    private var isConnected: Bool = false

    private var isTrackingInteraction = false
    private var isDragging = false
    private var initialScreenPoint = CGPoint.zero
    private var initialPanelOrigin = CGPoint.zero

    // MARK: - Constants

    private let ringSize: CGFloat = 124
    private let coreSize: CGFloat = GhostFabTokens.FabSize  // 110
    private let portraitSize: CGFloat = GhostFabTokens.FabSize  // 110 — fills core completely
    private let shadowSize: CGFloat = 140
    private let glowSize: CGFloat = 118  // Between core (110) and ring (124)
    private let dragThreshold: CGFloat = 6

    /// Stroke color shared by ring and core: white @ 25% alpha.
    private let fabStrokeColor = NSColor(srgbRed: 1, green: 1, blue: 1, alpha: 0.25).cgColor

    /// Bluish-white backlit color (matches GAIMER logo tint).
    private let backlitColor = NSColor(srgbRed: 0.7, green: 0.82, blue: 1.0, alpha: 1.0)

    // MARK: - Initialization

    public override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        wantsLayer = true
        layer?.masksToBounds = false  // Shadow extends beyond ring
        setupSubviews()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    // MARK: - Setup

    private func setupSubviews() {
        // 1. GhostShadow -- 140pt radial gradient, hidden by default
        ghostShadow = NSView()
        ghostShadow.wantsLayer = true
        guard let shadowLayer = ghostShadow.layer else { return }
        shadowGradient = GhostFabTokens.makeGhostShadowGradient()
        shadowLayer.addSublayer(shadowGradient)
        shadowLayer.cornerRadius = shadowSize / 2  // 70
        shadowLayer.masksToBounds = true
        shadowLayer.opacity = 0  // Hidden in closed state

        // 2. FabRing -- 124pt circle with SteelRing gradient
        fabRing = NSView()
        fabRing.wantsLayer = true
        guard let ringLayer = fabRing.layer else { return }
        ringGradient = GhostFabTokens.makeSteelRingGradient()
        ringLayer.addSublayer(ringGradient)
        ringLayer.cornerRadius = ringSize / 2  // 62
        ringLayer.masksToBounds = true
        ringLayer.borderWidth = 2
        ringLayer.borderColor = fabStrokeColor

        // 3. FabCore -- 110pt circle with GhostFill radial gradient
        fabCore = NSView()
        fabCore.wantsLayer = true
        guard let coreLayer = fabCore.layer else { return }
        coreGradient = GhostFabTokens.makeGhostFillGradient()
        coreLayer.addSublayer(coreGradient)
        coreLayer.cornerRadius = coreSize / 2  // 55
        coreLayer.masksToBounds = true
        coreLayer.borderWidth = 2
        coreLayer.borderColor = fabStrokeColor

        // 4. Backlit glow -- blurred colored circle behind portrait, off by default
        backlitGlow = CALayer()
        backlitGlow.backgroundColor = backlitColor.cgColor
        backlitGlow.cornerRadius = glowSize / 2
        backlitGlow.opacity = 0  // Hidden until active
        if let blur = CIFilter(name: "CIGaussianBlur", parameters: ["inputRadius": 8.0]) {
            backlitGlow.filters = [blur]
        }
        layer?.addSublayer(backlitGlow)

        // 5. FabPortrait -- CALayer for reliable circular clipping
        fabPortraitLayer = CALayer()
        fabPortraitLayer.contentsGravity = .resizeAspectFill
        fabPortraitLayer.contentsScale = NSScreen.main?.backingScaleFactor ?? 2.0
        fabPortraitLayer.cornerRadius = portraitSize / 2
        fabPortraitLayer.masksToBounds = true
        coreLayer.addSublayer(fabPortraitLayer)

        // --- Add to view hierarchy (bottom to top) ---
        addSubview(ghostShadow)   // Behind everything
        addSubview(fabRing)       // Ring behind core
        addSubview(fabCore)       // Core on top of ring
    }

    // MARK: - Layout

    override public func layout() {
        super.layout()

        // Ring centered in FabView
        let ringX = (bounds.width - ringSize) / 2
        let ringY = (bounds.height - ringSize) / 2
        fabRing.frame = NSRect(x: ringX, y: ringY, width: ringSize, height: ringSize)
        ringGradient.frame = fabRing.bounds

        // Core centered in FabView (same center as ring)
        let coreX = (bounds.width - coreSize) / 2
        let coreY = (bounds.height - coreSize) / 2
        fabCore.frame = NSRect(x: coreX, y: coreY, width: coreSize, height: coreSize)
        coreGradient.frame = fabCore.bounds

        // Backlit glow centered (same center, slightly larger than core)
        let glowX = (bounds.width - glowSize) / 2
        let glowY = (bounds.height - glowSize) / 2
        backlitGlow.frame = NSRect(x: glowX, y: glowY, width: glowSize, height: glowSize)

        // Portrait layer centered inside core
        let portraitX = (fabCore.bounds.width - portraitSize) / 2
        let portraitY = (fabCore.bounds.height - portraitSize) / 2
        fabPortraitLayer.frame = NSRect(x: portraitX, y: portraitY, width: portraitSize, height: portraitSize)
        fabPortraitLayer.cornerRadius = portraitSize / 2

        // Shadow centered on ring center
        let shadowX = (bounds.width - shadowSize) / 2
        let shadowY = (bounds.height - shadowSize) / 2
        ghostShadow.frame = NSRect(x: shadowX, y: shadowY, width: shadowSize, height: shadowSize)
        shadowGradient.frame = ghostShadow.bounds
    }

    // MARK: - Hit Testing

    public override func hitTest(_ point: NSPoint) -> NSView? {
        let localPoint = convert(point, from: superview)
        if isPointInRing(localPoint) {
            return self
        }
        return super.hitTest(point)
    }

    public override func mouseDown(with event: NSEvent) {
        let clickPoint = convert(event.locationInWindow, from: nil)
        guard isPointInRing(clickPoint) else {
            super.mouseDown(with: event)
            return
        }

        isTrackingInteraction = true
        isDragging = false
        initialScreenPoint = NSEvent.mouseLocation
        initialPanelOrigin = window?.frame.origin ?? .zero
    }

    public override func mouseDragged(with event: NSEvent) {
        guard isTrackingInteraction,
              let panel = window as? GhostPanel else {
            super.mouseDragged(with: event)
            return
        }

        let currentPoint = NSEvent.mouseLocation
        let dx = currentPoint.x - initialScreenPoint.x
        let dy = currentPoint.y - initialScreenPoint.y

        if !isDragging && hypot(dx, dy) < dragThreshold {
            return
        }

        isDragging = true
        panel.repositionForDrag(
            proposedOrigin: CGPoint(
                x: initialPanelOrigin.x + dx,
                y: initialPanelOrigin.y + dy
            )
        )
    }

    public override func mouseUp(with event: NSEvent) {
        guard isTrackingInteraction else {
            super.mouseUp(with: event)
            return
        }

        defer {
            isTrackingInteraction = false
            isDragging = false
        }

        if isDragging {
            (window as? GhostPanel)?.persistCurrentPlacement()
            return
        }

        let releasePoint = convert(event.locationInWindow, from: nil)
        if isPointInRing(releasePoint) {
            tapCallback?()
        }
    }

    // MARK: - Public API

    /// Sets the agent portrait image displayed in the FAB core.
    public func setAgentImage(_ image: NSImage?) {
        guard let image = image else {
            fabPortraitLayer.contents = nil
            return
        }
        var rect = NSRect(origin: .zero, size: image.size)
        fabPortraitLayer.contents = image.cgImage(forProposedRect: &rect, context: nil, hints: nil)
    }

    /// Controls GhostShadow visibility (opacity 1 when spine open, 0 when closed).
    /// Snaps immediately -- use `fadeShadowOut(completion:)` for animated fade.
    public func setShadowVisible(_ visible: Bool) {
        ghostShadow.layer?.opacity = visible ? 1.0 : 0.0
    }

    /// Fades the ghost shadow out with 200ms CubicIn animation (matches MAUI GhostShadow.FadeTo).
    /// Uses the animator proxy inside an NSAnimationContext block so the opacity transition is smooth.
    /// - Parameter completion: Called after fade animation completes
    public func fadeShadowOut(completion: (() -> Void)? = nil) {
        let cubicIn = CAMediaTimingFunction(controlPoints: 0.55, 0.055, 0.675, 0.19)
        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.2
            context.timingFunction = cubicIn
            self.ghostShadow.animator().alphaValue = 0.0
        }, completionHandler: {
            self.ghostShadow.layer?.opacity = 0.0
            completion?()
        })
    }

    /// Sets the callback fired when the FAB is tapped.
    public func setTapCallback(_ callback: VoidCallback?) {
        self.tapCallback = callback
    }

    /// Updates the FAB active state.
    /// When active: ring border color brightens (SteelLight at full alpha).
    /// When inactive: ring returns to default FabCoreStrokeBrush (white@25%).
    public func setActive(_ active: Bool) {
        self.isActive = active
        updateRingAppearance()
    }

    /// Updates the FAB connected state.
    /// When connected: ring gets a subtle green tint on the border.
    /// When disconnected: ring returns to default appearance.
    public func setConnected(_ connected: Bool) {
        self.isConnected = connected
        updateRingAppearance()
    }

    // MARK: - Ring Appearance

    private func updateRingAppearance() {
        guard let ringLayer = fabRing.layer else { return }

        if isActive {
            ringLayer.borderColor = GhostFabTokens.SteelLight.cgColor
            ringLayer.borderWidth = 2.5
            backlitGlow.opacity = 0.85
        } else if isConnected {
            ringLayer.borderColor = GhostFabTokens.VoiceGreen.withAlphaComponent(0.6).cgColor
            ringLayer.borderWidth = 2
            backlitGlow.opacity = 0.4
        } else {
            ringLayer.borderColor = NSColor(srgbRed: 1, green: 1, blue: 1, alpha: 0.25).cgColor
            ringLayer.borderWidth = 2
            backlitGlow.opacity = 0
        }
    }

    private func isPointInRing(_ point: CGPoint) -> Bool {
        let ringRadius: CGFloat = 62
        let center = CGPoint(x: bounds.midX, y: bounds.midY)
        let dx = point.x - center.x
        let dy = point.y - center.y
        return sqrt(dx * dx + dy * dy) <= ringRadius
    }
}
