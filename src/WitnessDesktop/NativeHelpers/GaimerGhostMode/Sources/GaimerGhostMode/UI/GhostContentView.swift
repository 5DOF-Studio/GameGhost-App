//
//  GhostContentView.swift
//  GhostFab-AppKit
//
//  Root NSView for the ghost panel. Hosts the twin-case layout:
//  TopCase -> SpineCard -> BottomCase with FAB overlaying the left side.
//  Uses flipped coordinates (origin at top-left) for natural top-down layout.
//

import AppKit
import QuartzCore

private final class DragSurfaceView: NSView {
    private let dragThreshold: CGFloat = 6
    private var isTrackingInteraction = false
    private var isDragging = false
    private var initialScreenPoint = CGPoint.zero
    private var initialPanelOrigin = CGPoint.zero

    override func mouseDown(with event: NSEvent) {
        isTrackingInteraction = true
        isDragging = false
        initialScreenPoint = NSEvent.mouseLocation
        initialPanelOrigin = window?.frame.origin ?? .zero
    }

    override func mouseDragged(with event: NSEvent) {
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

    override func mouseUp(with event: NSEvent) {
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
        }
    }
}

public class GhostContentView: NSView {

    // MARK: - Child Views

    private var topCase: NSView!          // Container with CaseBrush gradient + border + top-corner rounding
    private var topCaseBg: CAGradientLayer! // CaseBrush gradient sublayer (stored for layout updates)
    private var topBar: CaseBarView!      // Metallic bar inside topCase

    private var bottomCase: NSView!          // Container with CaseBrush gradient + border + bottom-corner rounding
    private var bottomCaseBg: CAGradientLayer! // CaseBrush gradient sublayer (stored for layout updates)
    private var bottomBar: CaseBarView!      // Metallic bar inside bottomCase

    private var separatorView: NSView!           // 2pt horizontal line between TopCase and BottomCase
    private var separatorGradient: CAGradientLayer! // SeparatorBrush horizontal gradient

    private var spineCard: SpineCardView!      // Spine card between TopCase and BottomCase

    private var fabView: FabView!             // FAB overlay centered on separator, left-aligned

    private var audioToggles: [AudioToggleButton] = []  // 4 audio channel toggles in bottomBar

    private var vadMeter: VadMeterView!               // 12-bar VAD level meter in topBar

    // MARK: - Layout State

    /// Guard against recursive layout calls during animation-driven frame changes.
    /// SpineCardView.setFrameSize -> layoutContent -> panel.setFrame -> setFrameSize -> layoutContent.
    /// Boolean flag is definitive (no jitter from height-delta thresholds).
    private var isLayingOut = false

    // MARK: - Callback Storage

    /// Stored callback fired when a card is dismissed (auto-dismiss or manual).
    private var cardDismissCallback: VoidCallback?

    /// Stored callback for gear tap (no UI trigger in Codex design, stored for API compatibility).
    private var gearTapCallback: VoidCallback?

    // MARK: - Auto-Dismiss Timer

    /// DispatchSource timer for auto-dismissing non-alert cards after 5 seconds.
    private var autoDismissTimer: DispatchSourceTimer?

    // MARK: - Width Toggle State

    /// Whether the panel is in extended width mode (1260pt vs 630pt).
    private var isExtended: Bool = false

    // MARK: - Public Accessors

    /// The FAB view for forwarding API calls (setAgentImage, setShadowVisible).
    public var fab: FabView { fabView }

    /// The spine card view for forwarding content API calls.
    public var spine: SpineCardView { spineCard }

    // MARK: - Initialization

    override public init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        self.wantsLayer = true
        setupLayers()
        setupChildViews()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    // MARK: - Coordinate System

    /// Flipped coordinates (origin at top-left) to match MAUI layout order:
    /// TopCase at y=0, SpineCard below, BottomCase at bottom.
    override public var isFlipped: Bool { true }

    // MARK: - Layer Setup

    private func setupLayers() {
        guard let layer = self.layer else { return }
        layer.backgroundColor = NSColor.clear.cgColor
        layer.masksToBounds = false
    }

    // MARK: - Child View Setup

    private func setupChildViews() {
        // --- TopCase ---
        topCase = NSView()
        topCase.wantsLayer = true
        guard let tcLayer = topCase.layer else { return }

        // Corner rounding: top corners only (CALayer maxY = visual top)
        tcLayer.cornerRadius = GhostFabTokens.CaseCornerRadius
        tcLayer.maskedCorners = [.layerMinXMaxYCorner, .layerMaxXMaxYCorner]
        tcLayer.masksToBounds = true

        // CaseBrush background gradient
        topCaseBg = GhostFabTokens.makeCaseGradient()
        tcLayer.addSublayer(topCaseBg)

        // Border stroke (simplified from CaseStrokeBrush diagonal gradient)
        tcLayer.borderWidth = 1.0
        tcLayer.borderColor = NSColor(srgbRed: 1.0, green: 1.0, blue: 1.0, alpha: 0.15).cgColor

        // --- TopBar ---
        topBar = CaseBarView(position: .top)
        topCase.addSubview(topBar)

        // --- VAD Meter (subview of topBar) ---
        vadMeter = VadMeterView(frame: .zero)
        topBar.addSubview(vadMeter)

        // --- BottomCase ---
        bottomCase = NSView()
        bottomCase.wantsLayer = true
        guard let bcLayer = bottomCase.layer else { return }

        // Corner rounding: bottom corners only (CALayer minY = visual bottom)
        bcLayer.cornerRadius = GhostFabTokens.CaseCornerRadius
        bcLayer.maskedCorners = [.layerMinXMinYCorner, .layerMaxXMinYCorner]
        bcLayer.masksToBounds = true

        // CaseBrush background gradient
        bottomCaseBg = GhostFabTokens.makeCaseGradient()
        bcLayer.addSublayer(bottomCaseBg)

        // Border stroke (same as TopCase)
        bcLayer.borderWidth = 1.0
        bcLayer.borderColor = NSColor(srgbRed: 1.0, green: 1.0, blue: 1.0, alpha: 0.15).cgColor

        // --- BottomBar ---
        bottomBar = CaseBarView(position: .bottom)
        bottomCase.addSubview(bottomBar)

        // --- Audio Toggle Buttons (subviews of bottomBar) ---
        for (i, config) in AudioToggleButton.configurations.enumerated() {
            let toggle = AudioToggleButton(index: i, glyphText: config.glyph, accentColor: config.accent,
                                           iconOnName: config.iconOn, iconOffName: config.iconOff)
            audioToggles.append(toggle)
            bottomBar.addSubview(toggle)
        }

        // --- SpineCard ---
        spineCard = SpineCardView(frame: .zero)

        // --- CaseSeparator ---
        // A 2pt horizontal line between TopCase and BottomCase
        // Uses SeparatorBrush horizontal gradient (clear -> white@30% -> SteelLight -> white@30% -> clear)
        separatorView = DragSurfaceView()
        separatorView.wantsLayer = true
        guard let sepLayer = separatorView.layer else { return }
        separatorGradient = GhostFabTokens.makeSeparatorGradient()
        sepLayer.addSublayer(separatorGradient)
        sepLayer.opacity = 0.6  // Matches MAUI Opacity="0.6"

        // --- FAB Overlay ---
        // Added last to view hierarchy so it renders on top of both cases and separator
        fabView = FabView(frame: .zero)

        // --- Add to view hierarchy ---
        // Order: topCase, spineCard, bottomCase, separatorView, fabView (last = on top)
        addSubview(topCase)
        addSubview(spineCard)
        addSubview(bottomCase)
        addSubview(separatorView)
        addSubview(fabView)
    }

    // MARK: - Layout

    /// Recalculates child view positions within the content view.
    /// Called automatically on resize via `setFrameSize(_:)`.
    ///
    /// Layout (flipped coords, y=0 at top):
    ///   TopCase:    y=0,                              height=72
    ///   SpineCard:  y=66 (72-6, tucked under TopCase) height=variable (0 when collapsed)
    ///   Separator:  y=72+spineContribution,           height=2
    ///   BottomCase: y=74+spineContribution,            height=72
    ///   Total:      146pt collapsed, grows with spine
    public func layoutContent() {
        guard !isLayingOut else { return }
        isLayingOut = true
        defer { isLayingOut = false }

        let w = bounds.width   // Should be CaseWidth (630) set by GhostPanel
        let barH = GhostFabTokens.BarHeight          // 72
        let sepH = GhostFabTokens.SeparatorThickness  // 2
        let spineInset = GhostFabTokens.SpineInset    // 20
        let spineNegativeMargin: CGFloat = 6

        // TopCase: full width, bar height, at y=0 (flipped coords, so top)
        topCase.frame = NSRect(x: 0, y: 0, width: w, height: barH)
        topCaseBg.frame = topCase.bounds
        topBar.frame = topCase.bounds

        // VAD meter in topBar
        layoutVadMeter()

        // SpineCard: positioned with negative margins to tuck under bars
        // y = barH - spineNegativeMargin (tucks 6pt under TopCase)
        // width = w - (spineInset * 2) (630 - 40 = 590)
        let spineY = barH - spineNegativeMargin
        let spineW = w - (spineInset * 2)
        spineCard.frame = NSRect(x: spineInset, y: spineY, width: spineW, height: spineCard.frame.height)

        // Calculate spine contribution to layout
        // spineCard.frame.height is the current animated height (starts at 0)
        let spineContribution = max(0, spineCard.frame.height - (spineNegativeMargin * 2))

        // CaseSeparator: below TopCase + spine contribution
        separatorView.frame = NSRect(x: 0, y: barH + spineContribution, width: w, height: sepH)
        separatorGradient.frame = separatorView.bounds

        // BottomCase: below separator + spine contribution
        bottomCase.frame = NSRect(x: 0, y: barH + spineContribution + sepH, width: w, height: barH)
        bottomCaseBg.frame = bottomCase.bounds
        bottomBar.frame = bottomCase.bounds

        // Audio toggle buttons right-aligned in bottomBar
        layoutAudioToggles()

        // FAB overlay: centered on the original separator position.
        // The panel itself stays anchored to the display's top edge while the
        // spine grows downward, so the FAB remains in the intended top-right zone.
        let fabSize: CGFloat = 140
        let fabX: CGFloat = 20
        let fabY: CGFloat = barH - (fabSize / 2)  // 72 - 70 = 2 (unchanged)
        fabView.frame = NSRect(x: fabX, y: fabY, width: fabSize, height: fabSize)

        // Resize panel to match content height during spine animation while
        // preserving the panel's top edge, so the ghost does not drift down.
        if let panel = window as? GhostPanel {
            let newHeight = totalHeight
            let currentFrame = panel.frame
            panel.resizePreservingTop(width: currentFrame.width, height: newHeight)
            // Update content view frame to match panel
            self.frame = NSRect(x: 0, y: 0, width: currentFrame.width, height: newHeight)
        }
    }

    /// Total content height including expanded spine.
    /// Used by animation code to resize the panel when spine height changes.
    public var totalHeight: CGFloat {
        let barH = GhostFabTokens.BarHeight
        let sepH = GhostFabTokens.SeparatorThickness
        let spineNegativeMargin: CGFloat = 6
        let spineContribution = max(0, spineCard.frame.height - (spineNegativeMargin * 2))
        return barH * 2 + sepH + spineContribution
    }

    override public func setFrameSize(_ newSize: NSSize) {
        super.setFrameSize(newSize)
        layoutContent()
    }

    // MARK: - Audio Toggle Layout

    /// Positions audio toggle buttons right-aligned in the bottomBar.
    /// Layout: 16pt right margin, 10pt spacing between 43pt buttons.
    /// CaseBarView is NOT flipped (y=0 at bottom), so y centers vertically.
    private func layoutAudioToggles() {
        let barW = bottomBar.bounds.width
        let barH = bottomBar.bounds.height  // 72
        let toggleSize: CGFloat = 43
        let rightMargin: CGFloat = 16
        let spacing: CGFloat = 10
        let toggleY = (barH - toggleSize) / 2  // 14.5 (centered vertically)

        for (i, toggle) in audioToggles.enumerated() {
            // Buttons ordered left-to-right as 0,1,2,3 with 3 rightmost
            let x = barW - rightMargin - toggleSize - (CGFloat(3 - i) * (toggleSize + spacing))
            toggle.frame = NSRect(x: x, y: toggleY, width: toggleSize, height: toggleSize)
        }
    }

    // MARK: - VAD Meter Layout

    /// Positions the VAD meter in the topBar at 150pt left margin, bottom-aligned with 8pt margin.
    /// CaseBarView is NOT flipped (y=0 at bottom), so y=8 places meter 8pt from bottom edge.
    private func layoutVadMeter() {
        vadMeter.frame = NSRect(x: 175, y: 8, width: 93, height: 20)
    }

    // MARK: - Audio Toggle API

    /// Sets the callback fired when any audio toggle is tapped.
    /// The callback receives (index: Int32, isOn: Bool).
    public func setAudioToggleCallback(_ callback: AudioToggleCallback?) {
        for toggle in audioToggles {
            toggle.audioToggleCallback = callback
        }
    }

    /// Updates the on/off state of a specific audio toggle.
    /// - Parameters:
    ///   - index: Toggle index (0=VoiceChat, 1=VoiceCmd, 2=GameAudio, 3=AudioIn)
    ///   - isOn: New toggle state
    public func setAudioState(index: Int, isOn: Bool) {
        guard index >= 0 && index < audioToggles.count else { return }
        audioToggles[index].isOn = isOn
    }

    // MARK: - VAD Meter API

    /// Sets the VAD meter level (0.0 = silence, 1.0 = max).
    /// Maps to number of lit bars with green/yellow/red color coding.
    public func setVadLevel(_ level: CGFloat) {
        vadMeter.setLevel(level)
    }

    // MARK: - Spine Card API

    /// Shows the spine card with text content, orchestrating all related visual changes.
    /// Coordinates: spine animation + separator hide + ghost shadow show + panel resize.
    /// - Parameters:
    ///   - message: Text content to display
    ///   - title: Optional header title
    ///   - eventIcon: Optional header icon image
    ///   - fixedHeight: Optional fixed height (overrides calculated content height)
    public func showCard(message: String, title: String? = nil, eventIcon: NSImage? = nil, fixedHeight: CGFloat? = nil, isAlert: Bool = false) {
        // Set content
        spineCard.setHeader(icon: eventIcon, title: title)
        spineCard.setMessage(message)

        // Calculate height
        let height = fixedHeight ?? spineCard.calculateContentHeight()

        // Hide separator (matches MAUI CaseSeparator.IsVisible = false)
        separatorView.isHidden = true

        // Show ghost shadow behind FAB
        fabView.setShadowVisible(true)

        // Animate spine open
        spineCard.showSpine(height: height)

        // Auto-dismiss: non-alert cards dismiss after 5 seconds (DSM-01, DSM-02)
        // Cancel existing timer first (DSM-03: new content cancels pending timer)
        cancelAutoDismiss()
        if !isAlert {
            scheduleAutoDismiss(seconds: 5.0)
        }
    }

    /// Shows the spine card with an image, orchestrating all related visual changes.
    /// Coordinates: spine animation + separator hide + ghost shadow show + panel resize.
    /// - Parameters:
    ///   - image: Image to display (AspectFill)
    ///   - fixedHeight: Height for the image display
    public func showCardImage(image: NSImage, fixedHeight: CGFloat, isAlert: Bool = false) {
        // Hide separator
        separatorView.isHidden = true

        // Show ghost shadow
        fabView.setShadowVisible(true)

        // Animate spine open with image
        spineCard.showSpineImage(image: image, height: fixedHeight)

        // Auto-dismiss: non-alert cards dismiss after 5 seconds (DSM-01, DSM-02)
        cancelAutoDismiss()
        if !isAlert {
            scheduleAutoDismiss(seconds: 5.0)
        }
    }

    /// Hides the spine card, restoring separator and hiding shadow.
    /// Coordinates: spine collapse + separator restore + ghost shadow animated fade.
    public func hideCard() {
        cancelAutoDismiss()

        // Fade out ghost shadow with 200ms CubicIn animation via animator proxy
        // Uses fadeShadowOut which animates inside NSAnimationContext block (not snapped off)
        fabView.fadeShadowOut()

        // Collapse spine and restore separator after animation completes
        spineCard.hideSpine {
            self.separatorView.isHidden = false
        }
    }

    // MARK: - FAB API

    /// Sets the agent portrait image on the FAB.
    public func setAgentImage(_ image: NSImage?) {
        fabView.setAgentImage(image)
    }

    /// Sets the callback fired when FAB is tapped.
    public func setFabTapCallback(_ callback: VoidCallback?) {
        fabView.setTapCallback(callback)
    }

    /// Updates FAB active state (ring appearance change).
    public func setFabActive(_ active: Bool) {
        fabView.setActive(active)
    }

    /// Updates FAB connected state (ring border tint).
    public func setFabConnected(_ connected: Bool) {
        fabView.setConnected(connected)
    }

    /// Shows or hides the ghost shadow behind FAB.
    public func setGhostShadowVisible(_ visible: Bool) {
        fabView.setShadowVisible(visible)
    }

    // MARK: - Dismiss Callback API

    /// Sets the callback fired when a card is dismissed (auto-dismiss or manual).
    public func setCardDismissCallback(_ callback: VoidCallback?) {
        cardDismissCallback = callback
    }

    /// Sets the callback for gear tap events.
    /// Note: Codex design has no gear button -- stored for API compatibility only.
    public func setGearTapCallback(_ callback: VoidCallback?) {
        gearTapCallback = callback
    }

    // MARK: - Width Toggle

    /// Toggles panel width between 630pt (CaseWidth) and 1260pt (2x CaseWidth)
    /// with 400ms CubicInOut animation matching MAUI Easing.CubicInOut.
    public func toggleWidth() {
        isExtended = !isExtended
        let targetWidth: CGFloat = isExtended ? GhostFabTokens.CaseWidth * 2 : GhostFabTokens.CaseWidth
        let cubicInOut = CAMediaTimingFunction(controlPoints: 0.645, 0.045, 0.355, 1.0)

        guard let panel = window as? GhostPanel else { return }
        let currentFrame = panel.frame

        NSAnimationContext.runAnimationGroup({ context in
            context.duration = 0.4  // 400ms
            context.timingFunction = cubicInOut
            panel.animator().setFrame(
                NSRect(x: currentFrame.origin.x,
                       y: currentFrame.origin.y,
                       width: targetWidth,
                       height: currentFrame.height),
                display: true
            )
        })
    }

    // MARK: - Auto-Dismiss Timer

    /// Schedules auto-dismiss to collapse the spine after the given number of seconds.
    /// Timer fires on main queue. Cancels any existing timer first.
    private func scheduleAutoDismiss(seconds: Double) {
        cancelAutoDismiss()
        let timer = DispatchSource.makeTimerSource(queue: .main)
        timer.schedule(deadline: .now() + seconds)
        timer.setEventHandler { [weak self] in
            self?.dismissCardInternal()
        }
        timer.resume()
        autoDismissTimer = timer
    }

    /// Cancels any pending auto-dismiss timer.
    private func cancelAutoDismiss() {
        autoDismissTimer?.cancel()
        autoDismissTimer = nil
    }

    /// Internal dismiss: hides card and fires the dismiss callback.
    /// Called by auto-dismiss timer and by explicit dismiss requests.
    private func dismissCardInternal() {
        cancelAutoDismiss()
        hideCard()
        cardDismissCallback?()
    }

    /// Dismisses the current card (called by ghost_panel_dismiss_card export).
    /// Cancels auto-dismiss timer and fires the dismiss callback.
    public func dismissCard() {
        dismissCardInternal()
    }
}
