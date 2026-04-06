//
//  GhostFabStateController.swift
//  GhostFab-AppKit
//
//  Thin state owner for harnesses, tests, and future SDK adapters.
//

import Foundation

public final class GhostFabStateController {
    public var onStateChange: ((GhostFabPanelState) -> Void)?

    public private(set) var state: GhostFabPanelState {
        didSet {
            onStateChange?(state)
        }
    }

    public init(initialState: GhostFabPanelState = GhostFabPanelState()) {
        self.state = initialState
    }

    public func replaceState(with state: GhostFabPanelState) {
        self.state = state
    }

    public func update(_ mutate: (inout GhostFabPanelState) -> Void) {
        var next = state
        mutate(&next)
        state = next
    }

    public func setAudioState(_ audioState: GhostFabAudioState) {
        update { $0.audioState = audioState }
    }

    public func setFabState(active: Bool, connected: Bool) {
        update {
            $0.isFabActive = active
            $0.isFabConnected = connected
        }
    }

    public func setCardContent(_ cardContent: GhostFabCardContent) {
        update { $0.cardContent = cardContent }
    }

    public func setVadLevel(_ level: CGFloat) {
        update { $0.vadLevel = min(max(level, 0), 1) }
    }
}
