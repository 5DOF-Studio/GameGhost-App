//
//  GhostFabAudioChannel.swift
//  GhostFab-AppKit
//
//  Host-facing audio channel identifiers.
//

import Foundation

public enum GhostFabAudioChannel: Int, CaseIterable, Sendable {
    case voiceChat = 0
    case voiceCommand = 1
    case gameAudio = 2
    case audioIn = 3
}
