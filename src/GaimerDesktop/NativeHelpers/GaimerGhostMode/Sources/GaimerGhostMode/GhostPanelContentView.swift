// GhostPanelContentView.swift
// Root AppKit view for the ghost panel overlay.
//
// Composes FabButtonView at top-right with GearIconView as a badge overlay,
// plus EventCardView and AudioControlCardView stacked to the left.
//
// Layout: [cards] --10pt-- [FAB with gear badge] --16 pad--
// When both cards visible: audio (utility) on top, message below, 8pt gap.
// Cards and audio card display/dismiss independently.
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

    // Audio control state
    var isMicActive: Bool = false
    var isCommentaryActive: Bool = false
    var isAiMicActive: Bool = false

    var onFabTap: (() -> Void)?
    var onCardDismiss: (() -> Void)?
    var onGearTap: (() -> Void)?
    var onAudioToggle: ((Int32, Bool) -> Void)?
}

protocol GhostPanelStateDelegate: AnyObject {
    func stateDidChange(_ state: GhostPanelState)
}

// MARK: - GhostPanelContentView

/// Root AppKit view for the ghost panel.
/// FAB at top-right with gear badge overlay. Cards stack to the left
/// with 10pt clearance from FAB. Audio card always above message card.
class GhostPanelContentView: NSView, GhostPanelStateDelegate {
    private let state: GhostPanelState
    private let fabView: FabButtonView
    private let gearView: GearIconView
    private var cardView: EventCardNSView?
    private var audioCardView: AudioControlCardView?

    /// Auto-dismiss timer for text/image cards
    private var autoDismissTimer: DispatchSourceTimer?
    /// Auto-dismiss timer for audio card
    private var audioAutoDismissTimer: DispatchSourceTimer?

    private let fabSize: CGFloat = 56
    private let edgePadding: CGFloat = 16
    private let gearSize: CGFloat = 24
    private let cardGap: CGFloat = 14    // clearance between FAB and cards
    private let stackGap: CGFloat = 12   // gap between stacked cards

    /// Custom FAB center set by dragging. nil = default top-right.
    private var customFabCenter: NSPoint?

    init(state: GhostPanelState) {
        self.state = state
        self.fabView = FabButtonView(
            agentImage: state.cachedAgentImage,
            isActive: state.isFabActive,
            isConnected: state.isFabConnected
        )
        self.gearView = GearIconView()
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

        // Gear added AFTER fab so it renders on top (badge overlay)
        gearView.onTap = { [weak self] in
            self?.handleGearTap()
        }
        addSubview(gearView)
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
        layoutFabAndGear()
        layoutAllCards()
    }

    /// Position FAB and gear badge.
    private func layoutFabAndGear() {
        let origin = fabOrigin
        fabView.frame = NSRect(x: origin.x, y: origin.y, width: fabSize, height: fabSize)

        // Gear badge: top-left corner of FAB, overlapping the circle edge
        // In AppKit (non-flipped): y increases upward, so top = origin.y + fabSize
        let gearX = origin.x - gearSize * 0.15
        let gearY = origin.y + fabSize - gearSize * 0.85
        gearView.frame = NSRect(x: gearX, y: gearY, width: gearSize, height: gearSize)
    }

    /// Positions all visible cards to the LEFT of the FAB with guaranteed clearance.
    /// Audio card always above message card. Stack vertically centered on FAB.
    ///
    /// Key invariant: a card's right edge NEVER exceeds `fabOrigin.x - cardGap`.
    /// If the FAB is dragged so far left that a card can't fit at full width,
    /// the card shrinks horizontally rather than extending under the FAB.
    ///
    /// Strips in-flight animations so frames update immediately on drag.
    private func layoutAllCards() {
        let origin = fabOrigin
        let fabCenterY = origin.y + fabSize / 2

        // The absolute right boundary for any card — guaranteed FAB clearance.
        let cardsRightEdge = origin.x - cardGap
        // Minimum usable width before we hide the card entirely
        let minCardWidth: CGFloat = 100

        // Collect visible cards (top to bottom in stack = audio, message)
        var slots: [(view: NSView, desiredWidth: CGFloat, height: CGFloat)] = []

        if let audio = audioCardView {
            let size = audio.intrinsicContentSize
            slots.append((audio, size.width, size.height))
        }
        if let msg = cardView {
            let size = msg.intrinsicContentSize
            slots.append((msg, size.width, size.height))
        }

        guard !slots.isEmpty else { return }

        // Available horizontal space for cards
        let availableWidth = cardsRightEdge - edgePadding

        // Total stack height
        let totalHeight = slots.reduce(0) { $0 + $1.height }
            + CGFloat(max(slots.count - 1, 0)) * stackGap

        // Center the stack vertically on FAB center (AppKit: y=0 at bottom)
        var currentY = fabCenterY + totalHeight / 2  // top of stack

        for slot in slots {
            currentY -= slot.height

            // Right-align to cardsRightEdge. Shrink width if needed.
            let actualWidth = min(slot.desiredWidth, max(availableWidth, 0))

            if actualWidth < minCardWidth {
                // Not enough room — hide card rather than render a sliver
                slot.view.isHidden = true
            } else {
                slot.view.isHidden = false
                let cardX = cardsRightEdge - actualWidth
                let clampedY = min(max(currentY, edgePadding),
                                   bounds.height - slot.height - edgePadding)

                // Strip in-flight animations so the frame takes effect immediately.
                slot.view.layer?.removeAllAnimations()
                slot.view.frame = NSRect(x: cardX, y: clampedY,
                                         width: actualWidth, height: slot.height)
                slot.view.alphaValue = 1
            }

            currentY -= stackGap
        }
    }

    // MARK: - Drag Handling

    private func handleFabDrag(to center: NSPoint) {
        // Clamp within bounds
        let halfFab = fabSize / 2
        let clampedX = min(max(center.x, halfFab + edgePadding), bounds.width - halfFab - edgePadding)
        let clampedY = min(max(center.y, halfFab + edgePadding), bounds.height - halfFab - edgePadding)
        customFabCenter = NSPoint(x: clampedX, y: clampedY)

        // Reposition everything immediately (no animation)
        layoutFabAndGear()
        layoutAllCards()
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

    // MARK: - Gear Tap Handling

    private func handleGearTap() {
        if audioCardView != nil {
            // Audio card visible — hide it
            hideAudioCard(animated: true)
        } else {
            // Show audio card (does NOT dismiss message card)
            showAudioControlCard()
        }
        // Notify C# that gear was tapped
        state.onGearTap?()
    }

    // MARK: - State Updates

    func stateDidChange(_ state: GhostPanelState) {
        fabView.update(
            agentImage: state.cachedAgentImage,
            isActive: state.isFabActive,
            isConnected: state.isFabConnected
        )

        // Show/hide message card
        if state.cardVariant != 0 {
            showCard()
        } else {
            hideCard(animated: true)
        }
    }

    /// Called when C# pushes new audio toggle state.
    func audioStateDidChange() {
        audioCardView?.updateToggleStates(
            micActive: state.isMicActive,
            commentaryActive: state.isCommentaryActive,
            aiMicActive: state.isAiMicActive
        )
    }

    // MARK: - Message Card

    private func showCard() {
        // Remove existing message card (replace with new content)
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

        // Position card at its final location via the unified layout engine,
        // then animate alpha fade-in only. layoutAllCards() is the single source
        // of truth for position — never set frame independently.
        card.alphaValue = 0
        layoutAllCards()

        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.25
            ctx.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
            card.animator().alphaValue = 1
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
                // Re-center remaining audio card if visible
                self?.layoutAllCards()
            })
        } else {
            card.removeFromSuperview()
            cardView = nil
            layoutAllCards()
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

    // MARK: - Audio Control Card

    func showAudioControlCard() {
        // Remove existing audio card (replace)
        cancelAudioAutoDismiss()
        audioCardView?.removeFromSuperview()

        let card = AudioControlCardView(
            micActive: state.isMicActive,
            commentaryActive: state.isCommentaryActive,
            aiMicActive: state.isAiMicActive
        )
        card.onToggleChanged = { [weak self] index, newValue in
            self?.state.onAudioToggle?(index, newValue)
        }
        card.onDismiss = { [weak self] in
            self?.hideAudioCard(animated: true)
        }
        audioCardView = card
        addSubview(card)

        // Position via the unified layout engine, animate alpha only.
        card.alphaValue = 0
        layoutAllCards()

        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.25
            ctx.timingFunction = CAMediaTimingFunction(name: .easeInEaseOut)
            card.animator().alphaValue = 1
        }

        // Auto-dismiss after 8 seconds
        scheduleAudioAutoDismiss(seconds: 8)
    }

    func hideAudioCard(animated: Bool) {
        cancelAudioAutoDismiss()
        guard let card = audioCardView else { return }

        if animated {
            NSAnimationContext.runAnimationGroup({ ctx in
                ctx.duration = 0.2
                card.animator().alphaValue = 0
            }, completionHandler: { [weak self] in
                card.removeFromSuperview()
                if self?.audioCardView === card {
                    self?.audioCardView = nil
                }
                // Re-center remaining message card if visible
                self?.layoutAllCards()
            })
        } else {
            card.removeFromSuperview()
            audioCardView = nil
            layoutAllCards()
        }
    }

    private func scheduleAudioAutoDismiss(seconds: Double) {
        cancelAudioAutoDismiss()
        let timer = DispatchSource.makeTimerSource(queue: .main)
        timer.schedule(deadline: .now() + seconds)
        timer.setEventHandler { [weak self] in
            self?.hideAudioCard(animated: true)
        }
        timer.resume()
        audioAutoDismissTimer = timer
    }

    private func cancelAudioAutoDismiss() {
        audioAutoDismissTimer?.cancel()
        audioAutoDismissTimer = nil
    }
}
