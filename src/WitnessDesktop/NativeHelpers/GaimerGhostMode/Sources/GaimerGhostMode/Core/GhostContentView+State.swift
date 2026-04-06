//
//  GhostContentView+State.swift
//  GhostFab-AppKit
//
//  Applies SDK-facing state models onto the existing AppKit view tree.
//

import AppKit

public extension GhostContentView {
    func apply(panelState: GhostFabPanelState) {
        if let image = panelState.agentImage {
            setAgentImage(image)
        }

        setFabActive(panelState.isFabActive)
        setFabConnected(panelState.isFabConnected)
        setVadLevel(panelState.vadLevel)

        for channel in GhostFabAudioChannel.allCases {
            setAudioState(index: channel.rawValue, isOn: panelState.audioState[channel])
        }

        switch panelState.cardContent {
        case .none:
            hideCard()
        case let .text(title, message, eventIcon, fixedHeight, isAlert):
            showCard(
                message: message,
                title: title,
                eventIcon: eventIcon,
                fixedHeight: fixedHeight,
                isAlert: isAlert
            )
        case let .image(_, image, fixedHeight, isAlert):
            showCardImage(image: image, fixedHeight: fixedHeight, isAlert: isAlert)
        }
    }
}
