//
//  GhostFabPanelRuntime.swift
//  GhostFab-AppKit
//
//  Central runtime state for the native compatibility bridge.
//  Keeps panel/window/callback lifetime separate from the @_cdecl exports.
//

import AppKit
import Foundation

func ghostFabRunOnMainSync<T>(_ block: () -> T) -> T {
    if Thread.isMainThread {
        return block()
    }
    var result: T!
    DispatchQueue.main.sync {
        result = block()
    }
    return result
}

final class GhostFabPanelRuntime {
    var panel: GhostPanel?
    var fabTapCallback: VoidCallback?
    var cardDismissCallback: VoidCallback?
    var gearTapCallback: VoidCallback?
    var audioToggleCallback: AudioToggleCallback?
    var hiddenHostWindow: NSWindow?
    var hiddenHostWindowNumber: Int?

    func contentView() -> GhostContentView? {
        panel?.contentView as? GhostContentView
    }

    @discardableResult
    func createPanel() -> Bool {
        panel = GhostPanel.create()
        return panel != nil
    }

    func reset() {
        panel?.hidePanel()
        panel = nil
        fabTapCallback = nil
        cardDismissCallback = nil
        gearTapCallback = nil
        audioToggleCallback = nil
        hiddenHostWindow = nil
        hiddenHostWindowNumber = nil
    }
}

let sharedRuntime = GhostFabPanelRuntime()
