//
//  CaseBarView.swift
//  GhostFab-AppKit
//
//  Reusable metallic bar view for TopBar and BottomBar.
//  Renders: BarBrush gradient + position-appropriate highlight overlay + border stroke.
//  Source of truth: DesignReference/GhostFabCodex/UI/Views/CodexRendererView.xaml
//

import AppKit
import QuartzCore

/// Which corners to round on the bar and its parent case.
enum CaseBarPosition {
    case top    // rounds top-left, top-right corners
    case bottom // rounds bottom-left, bottom-right corners
}

/// Reusable metallic bar view used for TopBar and BottomBar.
/// Renders: BarBrush gradient background + highlight overlay + border stroke.
/// Frame-based layout — parent sets this view's frame directly.
public class CaseBarView: NSView {
    private let dragThreshold: CGFloat = 6
    private var isTrackingInteraction = false
    private var isDragging = false
    private var initialScreenPoint = CGPoint.zero
    private var initialPanelOrigin = CGPoint.zero

    private let position: CaseBarPosition
    private var barGradient: CAGradientLayer!
    private var highlightGradient: CAGradientLayer!

    // MARK: - Initialization

    init(position: CaseBarPosition) {
        self.position = position
        super.init(frame: .zero)
        wantsLayer = true
        setupLayers()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    // MARK: - Layer Setup

    private func setupLayers() {
        guard let layer = self.layer else { return }

        // Corner rounding based on position
        layer.cornerRadius = GhostFabTokens.CaseCornerRadius
        switch position {
        case .top:
            // CALayer maxY = visual top edge
            layer.maskedCorners = [.layerMinXMaxYCorner, .layerMaxXMaxYCorner]
        case .bottom:
            // CALayer minY = visual bottom edge
            layer.maskedCorners = [.layerMinXMinYCorner, .layerMaxXMinYCorner]
        }
        layer.masksToBounds = true

        // Border stroke: simplified from CaseStrokeBrush diagonal gradient
        // to solid semi-transparent white (~15% alpha)
        layer.borderWidth = 1.0
        layer.borderColor = NSColor(srgbRed: 1.0, green: 1.0, blue: 1.0, alpha: 0.15).cgColor

        // BarBrush gradient background (SteelDark -> SteelMid -> SteelLight -> SteelMid -> SteelDark)
        barGradient = GhostFabTokens.makeBarGradient()
        layer.addSublayer(barGradient)

        // Highlight overlay: position-appropriate gradient at 0.9 opacity
        switch position {
        case .top:
            highlightGradient = GhostFabTokens.makeHighlightGradient()
        case .bottom:
            highlightGradient = GhostFabTokens.makeHighlightBottomGradient()
        }
        highlightGradient.opacity = 0.9
        layer.addSublayer(highlightGradient)
    }

    // MARK: - Layout

    override public func layout() {
        super.layout()
        barGradient.frame = bounds
        highlightGradient.frame = bounds
    }

    // MARK: - Drag Handling

    override public func mouseDown(with event: NSEvent) {
        isTrackingInteraction = true
        isDragging = false
        initialScreenPoint = NSEvent.mouseLocation
        initialPanelOrigin = window?.frame.origin ?? .zero
    }

    override public func mouseDragged(with event: NSEvent) {
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

    override public func mouseUp(with event: NSEvent) {
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
