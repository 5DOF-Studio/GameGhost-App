//
//  GhostFabExports.swift
//  GhostFab-AppKit
//
//  All 19 @_cdecl exports for C# P/Invoke interop.
//  Visibility, placement, and host-window restoration use ghostFabRunOnMainSync
//  so the Mar 24, 2026 ordering fixes are preserved during the Codex port.
//  String parameters converted immediately before dispatching to main queue,
//  because the pointer may be freed by .NET after the @_cdecl function returns.
//

import AppKit
import Foundation

// MARK: - Panel Lifecycle

/// Export 1: Creates the ghost panel singleton. Returns true on success.
/// Uses runOnMainSync because it must return a Bool to C#.
@_cdecl("ghost_panel_create")
public func ghostPanelCreate() -> Bool {
    sharedGhostFabSDK.createPanel()
}

/// Export 2: Destroys the ghost panel and clears all stored state.
@_cdecl("ghost_panel_destroy")
public func ghostPanelDestroy() {
    sharedGhostFabSDK.destroyPanel()
}

// MARK: - Visibility

/// Export 3: Shows the ghost panel (orderFrontRegardless).
@_cdecl("ghost_panel_show")
public func ghostPanelShow() {
    sharedGhostFabSDK.showPanel()
}

/// Export 4: Hides the ghost panel (orderOut).
@_cdecl("ghost_panel_hide")
public func ghostPanelHide() {
    sharedGhostFabSDK.hidePanel()
}

/// Export 5: Hides the MAUI/Catalyst host window using scored UINSWindow selection.
/// Stores both the window reference and windowNumber for robust restore.
@_cdecl("ghost_panel_hide_host_window")
public func ghostPanelHideHostWindow() {
    sharedGhostFabSDK.hideHostWindow()
}

/// Export 19: Restores the hidden MAUI/Catalyst host window using the stored
/// window reference, windowNumber fallback, then scored UINSWindow selection.
@_cdecl("ghost_panel_show_host_window")
public func ghostPanelShowHostWindow() {
    sharedGhostFabSDK.showHostWindow()
}

// MARK: - Content Management

/// Export 6: Shows a card with variant-based content routing.
/// Variant: 0=None(dismiss), 1=Voice(text), 2=Text, 3=TextWithImage.
/// All string pointers converted BEFORE async block (.NET frees buffer after return).
@_cdecl("ghost_panel_show_card")
public func ghostPanelShowCard(
    variant: Int32,
    titlePtr: UnsafePointer<CChar>?,
    textPtr: UnsafePointer<CChar>?,
    imagePathPtr: UnsafePointer<CChar>?,
    isAlert: Bool,
    isVoiceDelivered: Bool
) {
    // Convert strings BEFORE async block (pointer freed by .NET after return)
    let title: String? = titlePtr.map { String(cString: $0) }
    let text: String? = textPtr.map { String(cString: $0) }
    let imagePath: String? = imagePathPtr.map { String(cString: $0) }
    // Codex no longer renders the legacy "voice delivered" phone badge.
    // Keep the parameter for C# ABI compatibility even though presentation ignores it.
    let _ = isVoiceDelivered

    DispatchQueue.main.async {
        switch variant {
        case 0:
            sharedGhostFabSDK.dismissCard()
        case 1, 2:
            sharedGhostFabSDK.showTextCard(
                title: title,
                message: text ?? "",
                isAlert: isAlert
            )
        case 3:
            if let path = imagePath, let image = NSImage(contentsOfFile: path) {
                sharedGhostFabSDK.showImageCard(
                    title: title,
                    image: image,
                    fixedHeight: 200,
                    isAlert: isAlert
                )
            } else {
                sharedGhostFabSDK.showTextCard(
                    title: title,
                    message: text ?? "",
                    isAlert: isAlert
                )
            }
        default:
            break
        }
    }
}

/// Export 7: Dismisses the current card (cancels auto-dismiss, fires callback).
@_cdecl("ghost_panel_dismiss_card")
public func ghostPanelDismissCard() {
    DispatchQueue.main.async {
        sharedGhostFabSDK.dismissCard()
    }
}

/// Export 8: Sets the agent portrait image from a file path.
/// Path string converted BEFORE async block.
@_cdecl("ghost_panel_set_agent_image")
public func ghostPanelSetAgentImage(pathPtr: UnsafePointer<CChar>) {
    let path = String(cString: pathPtr)  // Convert BEFORE async
    DispatchQueue.main.async {
        sharedGhostFabSDK.setAgentImage(NSImage(contentsOfFile: path))
    }
}

/// Export 9: Sets FAB active state (ring appearance change).
@_cdecl("ghost_panel_set_fab_active")
public func ghostPanelSetFabActive(active: Bool) {
    DispatchQueue.main.async {
        sharedGhostFabSDK.setFabActive(active)
    }
}

/// Export 10: Sets FAB connected state (ring border tint).
@_cdecl("ghost_panel_set_fab_connected")
public func ghostPanelSetFabConnected(connected: Bool) {
    DispatchQueue.main.async {
        sharedGhostFabSDK.setFabConnected(connected)
    }
}

// MARK: - Positioning

/// Export 11: Moves the panel origin to the given screen coordinate.
@_cdecl("ghost_panel_set_position")
public func ghostPanelSetPosition(x: Double, y: Double) {
    sharedGhostFabSDK.setPosition(CGPoint(x: x, y: y))
}

/// Export 12: Resizes the panel to the given dimensions.
@_cdecl("ghost_panel_set_size")
public func ghostPanelSetSize(width: Double, height: Double) {
    sharedGhostFabSDK.setSize(CGSize(width: width, height: height))
}

// MARK: - Audio

/// Export 13: Sets all 4 audio toggle button states at once.
@_cdecl("ghost_panel_set_audio_state")
public func ghostPanelSetAudioState(
    voiceChatActive: Bool,
    voiceCommandActive: Bool,
    gameAudioActive: Bool,
    audioInActive: Bool
) {
    sharedGhostFabSDK.setAudioState(
        GhostFabAudioState(
            voiceChat: voiceChatActive,
            voiceCommand: voiceCommandActive,
            gameAudio: gameAudioActive,
            audioIn: audioInActive
        )
    )
}

/// Export 14: Updates the VAD meter level (0.0-1.0).
/// No NSLog -- called at ~15fps during voice chat.
@_cdecl("ghost_panel_set_vad_level")
public func ghostPanelSetVadLevel(level: Float) {
    DispatchQueue.main.async {
        sharedGhostFabSDK.setVadLevel(CGFloat(level))
    }
}

// MARK: - Callback Setters

/// Export 15: Sets the FAB tap callback. Stored at module level for lifetime safety.
@_cdecl("ghost_panel_set_fab_tap_callback")
public func ghostPanelSetFabTapCallback(callback: @convention(c) () -> Void) {
    sharedGhostFabSDK.setFabTapCallback(callback)
}

/// Export 16: Sets the card dismiss callback. Stored at module level for lifetime safety.
@_cdecl("ghost_panel_set_card_dismiss_callback")
public func ghostPanelSetCardDismissCallback(callback: @convention(c) () -> Void) {
    sharedGhostFabSDK.setCardDismissCallback(callback)
}

/// Export 17: Sets the gear tap callback. Stored at module level for lifetime safety.
/// Note: Codex design has no gear button -- stored for API compatibility.
@_cdecl("ghost_panel_set_gear_tap_callback")
public func ghostPanelSetGearTapCallback(callback: @convention(c) () -> Void) {
    sharedGhostFabSDK.setGearTapCallback(callback)
}

/// Export 18: Sets the audio toggle callback. Stored at module level for lifetime safety.
/// Callback receives (index: Int32, isOn: Bool) for each toggle event.
@_cdecl("ghost_panel_set_audio_toggle_callback")
public func ghostPanelSetAudioToggleCallback(callback: @convention(c) (Int32, Bool) -> Void) {
    sharedGhostFabSDK.setAudioToggleCallback(callback)
}
