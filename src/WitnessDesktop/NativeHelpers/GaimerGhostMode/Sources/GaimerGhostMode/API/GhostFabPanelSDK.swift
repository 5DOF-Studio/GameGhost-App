//
//  GhostFabPanelSDK.swift
//  GhostFab-AppKit
//
//  Public Swift-facing facade for driving the GhostFab panel as an SDK.
//

import AppKit
import Foundation

public final class GhostFabPanelSDK {
    public let stateController: GhostFabStateController

    private let runtime: GhostFabPanelRuntime

    public convenience init(initialState: GhostFabPanelState = GhostFabPanelState()) {
        self.init(
            runtime: sharedRuntime,
            stateController: GhostFabStateController(initialState: initialState)
        )
    }

    init(runtime: GhostFabPanelRuntime, stateController: GhostFabStateController) {
        self.runtime = runtime
        self.stateController = stateController
        bindStateController()
    }

    @discardableResult
    public func createPanel() -> Bool {
        ghostFabRunOnMainSync {
            let didCreate = runtime.createPanel()
            syncRuntimeToContentView()
            runtime.contentView()?.apply(panelState: stateController.state)
            return didCreate
        }
    }

    public func destroyPanel() {
        ghostFabRunOnMainSync {
            self.runtime.reset()
        }
    }

    public func showPanel() {
        ghostFabRunOnMainSync {
            self.runtime.panel?.showPanel()
        }
    }

    public func hidePanel() {
        ghostFabRunOnMainSync {
            self.runtime.panel?.hidePanel()
        }
    }

    public func hideHostWindow() {
        ghostFabRunOnMainSync {
            guard self.runtime.hiddenHostWindow == nil,
                  let window = self.resolveHostWindow(preferVisible: true) else { return }
            self.runtime.hiddenHostWindow = window
            self.runtime.hiddenHostWindowNumber = window.windowNumber
            window.orderOut(nil)
        }
    }

    public func showHostWindow() {
        ghostFabRunOnMainSync {
            let fallbackWindow = NSApplication.shared.windows.first { window in
                window !== self.runtime.panel &&
                window.className.contains("UINSWindow") &&
                (self.runtime.hiddenHostWindowNumber == nil ||
                 window.windowNumber == self.runtime.hiddenHostWindowNumber)
            }

            guard let window = self.runtime.hiddenHostWindow
                    ?? fallbackWindow
                    ?? self.resolveHostWindow(preferVisible: false) else { return }

            window.orderFrontRegardless()
            window.makeKey()
            self.runtime.hiddenHostWindow = nil
            self.runtime.hiddenHostWindowNumber = nil
        }
    }

    public func setPosition(_ origin: CGPoint) {
        ghostFabRunOnMainSync {
            self.runtime.panel?.reposition(x: origin.x, y: origin.y)
        }
    }

    public func setSize(_ size: CGSize) {
        ghostFabRunOnMainSync {
            self.runtime.panel?.resize(width: size.width, height: size.height)
        }
    }

    public func replaceState(with state: GhostFabPanelState) {
        stateController.replaceState(with: state)
    }

    public func updateState(_ mutate: (inout GhostFabPanelState) -> Void) {
        stateController.update(mutate)
    }

    public func dismissCard() {
        updateState { $0.cardContent = .none }
    }

    public func showTextCard(
        title: String?,
        message: String,
        eventIcon: NSImage? = nil,
        fixedHeight: CGFloat? = nil,
        isAlert: Bool = false
    ) {
        updateState {
            $0.cardContent = .text(
                title: title,
                message: message,
                eventIcon: eventIcon,
                fixedHeight: fixedHeight,
                isAlert: isAlert
            )
        }
    }

    public func showImageCard(
        title: String? = nil,
        image: NSImage,
        fixedHeight: CGFloat,
        isAlert: Bool = false
    ) {
        updateState {
            $0.cardContent = .image(
                title: title,
                image: image,
                fixedHeight: fixedHeight,
                isAlert: isAlert
            )
        }
    }

    public func showVideoCard(
        title: String? = nil,
        fileURL: URL,
        startTime: TimeInterval,
        duration: TimeInterval
    ) {
        updateState {
            $0.cardContent = .video(
                title: title,
                fileURL: fileURL,
                startTime: startTime,
                duration: duration
            )
        }
    }

    public func setAgentImage(_ image: NSImage?) {
        updateState { $0.agentImage = image }
    }

    public func setFabActive(_ active: Bool) {
        updateState { $0.isFabActive = active }
    }

    public func setFabConnected(_ connected: Bool) {
        updateState { $0.isFabConnected = connected }
    }

    public func setAudioState(_ audioState: GhostFabAudioState) {
        stateController.setAudioState(audioState)
    }

    public func setVadLevel(_ level: CGFloat) {
        stateController.setVadLevel(level)
    }

    public func setFabTapCallback(_ callback: VoidCallback?) {
        runtime.fabTapCallback = callback
        DispatchQueue.main.async {
            self.runtime.contentView()?.setFabTapCallback(callback)
        }
    }

    public func setCardDismissCallback(_ callback: VoidCallback?) {
        runtime.cardDismissCallback = callback
        DispatchQueue.main.async {
            self.runtime.contentView()?.setCardDismissCallback(callback)
        }
    }

    public func setGearTapCallback(_ callback: VoidCallback?) {
        runtime.gearTapCallback = callback
        DispatchQueue.main.async {
            self.runtime.contentView()?.setGearTapCallback(callback)
        }
    }

    public func setAudioToggleCallback(_ callback: AudioToggleCallback?) {
        runtime.audioToggleCallback = callback
        DispatchQueue.main.async {
            self.runtime.contentView()?.setAudioToggleCallback(callback)
        }
    }

    private func bindStateController() {
        stateController.onStateChange = { [weak self] state in
            DispatchQueue.main.async {
                self?.runtime.contentView()?.apply(panelState: state)
            }
        }
    }

    private func syncRuntimeToContentView() {
        guard let contentView = runtime.contentView() else { return }
        contentView.setFabTapCallback(runtime.fabTapCallback)
        contentView.setCardDismissCallback(runtime.cardDismissCallback)
        contentView.setGearTapCallback(runtime.gearTapCallback)
        contentView.setAudioToggleCallback(runtime.audioToggleCallback)
    }

    private func resolveHostWindow(preferVisible: Bool) -> NSWindow? {
        let candidates = NSApplication.shared.windows.filter { window in
            window !== runtime.panel && window.className.contains("UINSWindow")
        }

        let ordered = candidates.sorted { lhs, rhs in
            let lhsScore = (lhs.isKeyWindow ? 4 : 0) + (lhs.isMainWindow ? 2 : 0) + (lhs.isVisible ? 1 : 0)
            let rhsScore = (rhs.isKeyWindow ? 4 : 0) + (rhs.isMainWindow ? 2 : 0) + (rhs.isVisible ? 1 : 0)
            return lhsScore > rhsScore
        }

        if preferVisible, let visible = ordered.first(where: \.isVisible) {
            return visible
        }

        return ordered.first ?? NSApplication.shared.windows.first { $0 !== runtime.panel }
    }
}

let sharedGhostFabSDK = GhostFabPanelSDK(
    runtime: sharedRuntime,
    stateController: GhostFabStateController()
)
