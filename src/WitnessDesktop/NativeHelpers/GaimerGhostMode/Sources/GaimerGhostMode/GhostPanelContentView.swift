// GhostPanelContentView.swift
// Root AppKit view for the ghost panel overlay.
//
// Composes FabButtonView at top-right and EventCardView below it.
// Bound to GhostPanelState which is updated from @_cdecl functions
// via the C# P/Invoke bridge.
//
// PURE APPKIT — no SwiftUI. SwiftUI's NSHostingView is not available
// in macOS frameworks loaded into Mac Catalyst processes.

import AppKit

// MARK: - GhostPanelState

/// Central state shared between the @_cdecl exports and AppKit views.
/// All property mutations must happen on the main thread.
/// Uses a delegate pattern instead of SwiftUI's @Published.
class GhostPanelState {
    weak var delegate: GhostPanelStateDelegate?

    var agentImagePath: String? {
        didSet {
            if let path = agentImagePath {
                cachedAgentImage = NSImage(contentsOfFile: path)
            } else {
                cachedAgentImage = nil
            }
            delegate?.stateDidChange(self)
        }
    }

    var cachedAgentImage: NSImage?

    var isFabActive: Bool = false {
        didSet { delegate?.stateDidChange(self) }
    }

    var isFabConnected: Bool = false {
        didSet { delegate?.stateDidChange(self) }
    }

    /// Card variant: 0=None, 1=Voice, 2=Text, 3=TextWithImage
    var cardVariant: Int32 = 0 {
        didSet { delegate?.stateDidChange(self) }
    }

    var cardTitle: String?
    var cardText: String?
    var cardImagePath: String?

    var onFabTap: (() -> Void)?
    var onCardDismiss: (() -> Void)?
}

protocol GhostPanelStateDelegate: AnyObject {
    func stateDidChange(_ state: GhostPanelState)
}

// MARK: - GhostPanelContentView

/// Root AppKit view for the ghost panel.
/// Positions FAB at top-right (HUD style), event cards to the left of FAB
/// like a speech bubble. Background is fully transparent. Click-through for empty areas.
class GhostPanelContentView: NSView, GhostPanelStateDelegate {
    private let state: GhostPanelState
    private let fabView: FabButtonView
    private var cardView: EventCardNSView?

    /// Auto-dismiss timer for text/image cards
    private var autoDismissTimer: DispatchSourceTimer?

    private let fabSize: CGFloat = 56
    private let edgePadding: CGFloat = 16
    private let cardGap: CGFloat = 20

    /// Custom FAB center set by dragging. nil = default top-right.
    private var customFabCenter: NSPoint?

    init(state: GhostPanelState) {
        self.state = state
        self.fabView = FabButtonView(
            agentImage: state.cachedAgentImage,
            isActive: state.isFabActive,
            isConnected: state.isFabConnected
        )
        super.init(frame: .zero)

        state.delegate = self
        wantsLayer = true

        fabView.onTap = { [weak self] in
            self?.state.onFabTap?()
        }
        fabView.onDrag = { [weak self] newCenter in
            self?.handleFabDrag(to: newCenter)
        }
        addSubview(fabView)
    }

    required init?(coder: NSCoder) { fatalError() }

    override var isFlipped: Bool { false }

    /// Returns the current FAB origin (bottom-left corner in AppKit coords).
    private var fabOrigin: NSPoint {
        if let center = customFabCenter {
            return NSPoint(x: center.x - fabSize / 2, y: center.y - fabSize / 2)
        }
        // Default: top-right
        return NSPoint(
            x: bounds.width - fabSize - edgePadding,
            y: bounds.height - fabSize - edgePadding
        )
    }

    override func layout() {
        super.layout()

        let origin = fabOrigin
        fabView.frame = NSRect(x: origin.x, y: origin.y, width: fabSize, height: fabSize)

        if let card = cardView {
            layoutCard(card)
        }
    }

    private func layoutCard(_ card: EventCardNSView) {
        let cardWidth: CGFloat = 280
        let cardHeight = card.intrinsicContentSize.height
        let origin = self.fabOrigin

        // Card to the LEFT of FAB, vertically centered on FAB (speech bubble style)
        let cardX = origin.x - cardGap - cardWidth
        let fabCenterY = origin.y + fabSize / 2
        let cardY = fabCenterY - cardHeight / 2

        card.frame = NSRect(
            x: max(cardX, edgePadding),
            y: min(max(cardY, edgePadding), bounds.height - cardHeight - edgePadding),
            width: cardWidth,
            height: cardHeight
        )
    }

    // MARK: - Drag Handling

    private func handleFabDrag(to center: NSPoint) {
        // Clamp within bounds
        let halfFab = fabSize / 2
        let clampedX = min(max(center.x, halfFab + edgePadding), bounds.width - halfFab - edgePadding)
        let clampedY = min(max(center.y, halfFab + edgePadding), bounds.height - halfFab - edgePadding)
        customFabCenter = NSPoint(x: clampedX, y: clampedY)

        // Reposition FAB immediately (no animation)
        let origin = fabOrigin
        fabView.frame = NSRect(x: origin.x, y: origin.y, width: fabSize, height: fabSize)

        // Reposition card if visible
        if let card = cardView {
            layoutCard(card)
        }
    }

    // MARK: - Click-through

    override func hitTest(_ point: NSPoint) -> NSView? {
        let result = super.hitTest(point)
        // Pass through if nothing hit or if the root view itself was hit
        if result == nil || result === self {
            return nil
        }
        return result
    }

    // MARK: - State Updates

    func stateDidChange(_ state: GhostPanelState) {
        fabView.update(
            agentImage: state.cachedAgentImage,
            isActive: state.isFabActive,
            isConnected: state.isFabConnected
        )

        // Show/hide card
        if state.cardVariant != 0 {
            showCard()
        } else {
            hideCard(animated: true)
        }
    }

    private func showCard() {
        // Remove existing card
        cancelAutoDismiss()
        cardView?.removeFromSuperview()

        let card = EventCardNSView(
            variant: state.cardVariant,
            title: state.cardTitle,
            text: state.cardText,
            imagePath: state.cardImagePath,
            agentImage: state.cachedAgentImage
        )
        card.onDismiss = { [weak self] in
            self?.dismissCard()
        }
        cardView = card
        addSubview(card)

        // Calculate final position: to the LEFT of FAB, vertically centered
        let cardWidth: CGFloat = 280
        let cardHeight = card.intrinsicContentSize.height
        let origin = fabOrigin
        let finalX = max(origin.x - cardGap - cardWidth, edgePadding)
        let fabCenterY = origin.y + fabSize / 2
        let finalY = min(max(fabCenterY - cardHeight / 2, edgePadding), bounds.height - cardHeight - edgePadding)

        // Slide-in animation (from right, toward FAB)
        card.alphaValue = 0
        card.frame = NSRect(x: finalX + 40, y: finalY, width: cardWidth, height: cardHeight)
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.25
            ctx.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
            card.animator().alphaValue = 1
            card.animator().frame.origin.x = finalX
        }

        // Auto-dismiss for text/image cards (not voice)
        if state.cardVariant != 1 {
            scheduleAutoDismiss(seconds: 8)
        }
    }

    private func hideCard(animated: Bool) {
        cancelAutoDismiss()
        guard let card = cardView else { return }

        if animated {
            NSAnimationContext.runAnimationGroup({ ctx in
                ctx.duration = 0.2
                card.animator().alphaValue = 0
            }, completionHandler: { [weak self] in
                card.removeFromSuperview()
                if self?.cardView === card {
                    self?.cardView = nil
                }
            })
        } else {
            card.removeFromSuperview()
            cardView = nil
        }
    }

    private func dismissCard() {
        state.cardVariant = 0
        state.onCardDismiss?()
    }

    private func scheduleAutoDismiss(seconds: Double) {
        cancelAutoDismiss()
        let timer = DispatchSource.makeTimerSource(queue: .main)
        timer.schedule(deadline: .now() + seconds)
        timer.setEventHandler { [weak self] in
            self?.dismissCard()
        }
        timer.resume()
        autoDismissTimer = timer
    }

    private func cancelAutoDismiss() {
        autoDismissTimer?.cancel()
        autoDismissTimer = nil
    }
}
