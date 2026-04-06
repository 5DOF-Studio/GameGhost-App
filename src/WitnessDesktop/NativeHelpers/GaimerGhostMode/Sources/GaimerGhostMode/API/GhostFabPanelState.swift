//
//  GhostFabPanelState.swift
//  GhostFab-AppKit
//
//  SDK-facing state models for driving the panel without going through C exports.
//

import AppKit
import Foundation

public struct GhostFabAudioState: Sendable, Equatable {
    public var voiceChat: Bool
    public var voiceCommand: Bool
    public var gameAudio: Bool
    public var audioIn: Bool

    public init(
        voiceChat: Bool = false,
        voiceCommand: Bool = false,
        gameAudio: Bool = false,
        audioIn: Bool = false
    ) {
        self.voiceChat = voiceChat
        self.voiceCommand = voiceCommand
        self.gameAudio = gameAudio
        self.audioIn = audioIn
    }

    public subscript(channel: GhostFabAudioChannel) -> Bool {
        get {
            switch channel {
            case .voiceChat: return voiceChat
            case .voiceCommand: return voiceCommand
            case .gameAudio: return gameAudio
            case .audioIn: return audioIn
            }
        }
        set {
            switch channel {
            case .voiceChat: voiceChat = newValue
            case .voiceCommand: voiceCommand = newValue
            case .gameAudio: gameAudio = newValue
            case .audioIn: audioIn = newValue
            }
        }
    }
}

public enum GhostFabCardContent {
    case none
    case text(title: String?, message: String, eventIcon: NSImage? = nil, fixedHeight: CGFloat? = nil, isAlert: Bool = false)
    case image(title: String? = nil, image: NSImage, fixedHeight: CGFloat, isAlert: Bool = false)
}

public struct GhostFabPanelState {
    public var agentImage: NSImage?
    public var isFabActive: Bool
    public var isFabConnected: Bool
    public var audioState: GhostFabAudioState
    public var vadLevel: CGFloat
    public var cardContent: GhostFabCardContent

    public init(
        agentImage: NSImage? = nil,
        isFabActive: Bool = false,
        isFabConnected: Bool = false,
        audioState: GhostFabAudioState = GhostFabAudioState(),
        vadLevel: CGFloat = 0,
        cardContent: GhostFabCardContent = .none
    ) {
        self.agentImage = agentImage
        self.isFabActive = isFabActive
        self.isFabConnected = isFabConnected
        self.audioState = audioState
        self.vadLevel = vadLevel
        self.cardContent = cardContent
    }
}
