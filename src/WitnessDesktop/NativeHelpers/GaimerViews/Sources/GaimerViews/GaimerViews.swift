// GaimerViews.swift
// SwiftUI views exported as UIView via @_cdecl for P/Invoke consumption from .NET MAUI.
//
// BUILD TARGET: Mac Catalyst (NOT macOS) — SwiftUI works here via UIHostingController.
//
// Pattern:
//   1. Design your SwiftUI view normally
//   2. Wrap in UIHostingController
//   3. Export a @_cdecl factory that returns the UIView pointer
//   4. Export @_cdecl setter functions to push data from C#
//   5. Export @_cdecl callback registration for events back to C#

import SwiftUI
import UIKit

// MARK: - Example: Connector Display Card

/// A proof-of-concept SwiftUI card that can be designed in Xcode
/// and injected into MAUI views with zero rewrite.
struct ConnectorCard: View {
    @ObservedObject var state: ConnectorCardState

    var body: some View {
        HStack(spacing: 12) {
            // Status indicator
            Circle()
                .fill(state.isConnected ? Color.green : Color.gray)
                .frame(width: 10, height: 10)
                .overlay(
                    Circle()
                        .fill(state.isConnected ? Color.green : Color.clear)
                        .frame(width: 18, height: 18)
                        .opacity(0.3)
                )

            VStack(alignment: .leading, spacing: 2) {
                Text(state.title)
                    .font(.system(size: 14, weight: .semibold, design: .rounded))
                    .foregroundColor(.white)

                Text(state.subtitle)
                    .font(.system(size: 11, weight: .regular, design: .rounded))
                    .foregroundColor(.white.opacity(0.6))
            }

            Spacer()

            // Action icon
            Image(systemName: state.isConnected ? "link" : "link.badge.plus")
                .font(.system(size: 16))
                .foregroundColor(.white.opacity(0.5))
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
        .background(
            RoundedRectangle(cornerRadius: 14)
                .fill(Color.white.opacity(0.06))
                .overlay(
                    RoundedRectangle(cornerRadius: 14)
                        .strokeBorder(Color.white.opacity(0.1), lineWidth: 1)
                )
        )
    }
}

// MARK: - Example: Tool Status Card

/// Tool-use status card for ghost mode — centered icon + action phrase.
struct ToolStatusCard: View {
    @ObservedObject var state: ToolStatusCardState

    var body: some View {
        VStack(spacing: 8) {
            // Tool icon (SF Symbol or custom image)
            Image(systemName: state.iconName)
                .font(.system(size: 32, weight: .light))
                .foregroundColor(.white.opacity(0.7))

            Text(state.actionPhrase)
                .font(.system(size: 13, weight: .medium, design: .rounded))
                .foregroundColor(.white.opacity(0.5))

            if state.showDuration {
                Text(state.durationText)
                    .font(.system(size: 10, weight: .regular, design: .monospaced))
                    .foregroundColor(.white.opacity(0.3))
            }
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 16)
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(Color.white.opacity(0.04))
        )
    }
}

// MARK: - Observable State Objects

/// State object for ConnectorCard — C# pushes values via setter functions.
class ConnectorCardState: ObservableObject {
    @Published var title: String = "Chess.app"
    @Published var subtitle: String = "Disconnected"
    @Published var isConnected: Bool = false
}

/// State object for ToolStatusCard.
class ToolStatusCardState: ObservableObject {
    @Published var iconName: String = "wrench.and.screwdriver"
    @Published var actionPhrase: String = "Running tool..."
    @Published var durationText: String = ""
    @Published var showDuration: Bool = false
}

// MARK: - Singleton State Holders (C# pushes into these)

/// Global state instances that C# setter functions mutate.
/// SwiftUI views observe these via @ObservedObject.
private let connectorState = ConnectorCardState()
private let toolState = ToolStatusCardState()

// MARK: - @_cdecl Exports: View Factories

/// Creates a ConnectorCard SwiftUI view wrapped in UIView.
/// Returns an unmanaged pointer to the UIView.
/// C# must release via gaimer_views_release().
@_cdecl("gaimer_views_create_connector_card")
public func createConnectorCard() -> UnsafeMutableRawPointer {
    let hosting = UIHostingController(rootView: ConnectorCard(state: connectorState))
    hosting.view.backgroundColor = .clear
    // Size the view (MAUI handler will manage actual layout)
    hosting.view.frame = CGRect(x: 0, y: 0, width: 300, height: 56)
    return Unmanaged.passRetained(hosting.view).toOpaque()
}

/// Creates a ToolStatusCard SwiftUI view wrapped in UIView.
@_cdecl("gaimer_views_create_tool_card")
public func createToolCard() -> UnsafeMutableRawPointer {
    let hosting = UIHostingController(rootView: ToolStatusCard(state: toolState))
    hosting.view.backgroundColor = .clear
    hosting.view.frame = CGRect(x: 0, y: 0, width: 200, height: 100)
    return Unmanaged.passRetained(hosting.view).toOpaque()
}

// MARK: - @_cdecl Exports: State Setters

/// Update connector card state from C#.
@_cdecl("gaimer_views_set_connector_state")
public func setConnectorState(
    titlePtr: UnsafePointer<CChar>,
    subtitlePtr: UnsafePointer<CChar>,
    isConnected: Bool
) {
    let title = String(cString: titlePtr)
    let subtitle = String(cString: subtitlePtr)
    DispatchQueue.main.async {
        connectorState.title = title
        connectorState.subtitle = subtitle
        connectorState.isConnected = isConnected
    }
}

/// Update tool card state from C#.
@_cdecl("gaimer_views_set_tool_state")
public func setToolState(
    iconNamePtr: UnsafePointer<CChar>,
    actionPhrasePtr: UnsafePointer<CChar>,
    durationMs: Int32
) {
    let iconName = String(cString: iconNamePtr)
    let actionPhrase = String(cString: actionPhrasePtr)
    DispatchQueue.main.async {
        toolState.iconName = iconName
        toolState.actionPhrase = actionPhrase
        toolState.showDuration = durationMs > 0
        toolState.durationText = durationMs > 0 ? "\(durationMs)ms" : ""
    }
}

// MARK: - @_cdecl Exports: Lifecycle

/// Release a previously created view.
/// Call this when the MAUI handler is disposed.
@_cdecl("gaimer_views_release")
public func releaseView(viewPtr: UnsafeMutableRawPointer) {
    Unmanaged<UIView>.fromOpaque(viewPtr).release()
}

// MARK: - @_cdecl Exports: Tap Callbacks

/// Function pointer type for tap callbacks back to C#.
private var onConnectorTapCallback: (@convention(c) () -> Void)?

/// Register a C# callback for connector card tap.
@_cdecl("gaimer_views_set_connector_tap_callback")
public func setConnectorTapCallback(callback: @escaping @convention(c) () -> Void) {
    onConnectorTapCallback = callback
}

// To use tap callbacks from SwiftUI, add .onTapGesture to the view
// and call onConnectorTapCallback?() inside it.
