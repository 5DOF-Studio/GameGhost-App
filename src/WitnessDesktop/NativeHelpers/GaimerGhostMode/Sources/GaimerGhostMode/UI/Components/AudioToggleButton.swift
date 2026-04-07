//
//  AudioToggleButton.swift
//  GhostFab-AppKit
//
//  Circular audio toggle button that wraps CodexMetalBadge in a tappable container.
//  Four instances sit right-aligned in BottomBar for audio channel control.
//  Source of truth: DesignReference/GhostFabCodex/UI/Controls/AudioToggleControl.xaml
//

import AppKit
import QuartzCore

/// C-compatible callback for audio toggle events.
/// Parameters: (index: Int32, isOn: Bool) where index is 0-3.
public typealias AudioToggleCallback = @convention(c) (Int32, Bool) -> Void

/// Circular 43pt audio toggle button that contains a CodexMetalBadge subview.
/// Tapping toggles between muted (inactive) and accent-colored (active) states.
/// The button's own layer carries the shadow since the badge clips to a circle.
///
/// Visual states:
///   Inactive: glyph/stroke #2A2A3A, no glow, black offset shadow
///   Active:   glyph/stroke = accentColor, glow ring visible, accent glow shadow
public class AudioToggleButton: NSView {

    // MARK: - Static Configurations

    /// Glyph, accent color, and optional icon asset names (on/off) for each audio channel.
    /// Index 0=VoiceChat, 1=VoiceCmd, 2=GameAudio, 3=AudioIn.
    static let configurations: [(glyph: String, accent: NSColor, iconOn: String?, iconOff: String?)] = [
        ("\u{260F}", GhostFabTokens.VoiceGreen, "voice-chat-on", "voice-chat-off 1"),  // index 0: VoiceChat
        ("\u{3030}", GhostFabTokens.VoiceCommandBlue, "ai-speak", "ai-mute"),          // index 1: VoiceCmd
        ("\u{266A}", GhostFabTokens.GameAudioAmber, "audio-in-on", "audio-in-off"),     // index 2: GameAudio
        ("\u{25CF}", GhostFabTokens.MicRed, "ghost-mic-on", "ghost-mic-off")           // index 3: AudioIn
    ]

    // MARK: - Constants

    private static let buttonSize: CGFloat = 43
    private static let mutedColor = NSColor(srgbRed: 0x2A / 255.0,
                                            green: 0x2A / 255.0,
                                            blue: 0x3A / 255.0,
                                            alpha: 1.0)

    // MARK: - Properties

    /// Index of this toggle (0-3), identifies the audio channel.
    public let index: Int

    /// Accent color used when toggle is active.
    public let accentColor: NSColor

    /// Current toggle state. Setting this updates the visual appearance.
    public var isOn: Bool = false {
        didSet { applyToggleState() }
    }

    /// Callback fired when the toggle is tapped. Receives (index, newState).
    public var audioToggleCallback: AudioToggleCallback?

    /// Icon images for on and off states (nil = use text glyph).
    private var iconOn: NSImage?
    private var iconOff: NSImage?

    // MARK: - Subviews

    private let badge: CodexMetalBadge

    // MARK: - Initialization

    /// Creates an audio toggle button.
    /// - Parameters:
    ///   - index: Channel index (0-3)
    ///   - glyphText: Glyph character to display (fallback if no icon)
    ///   - accentColor: Color used in the active state
    ///   - iconOnName: Optional asset name for the on-state icon
    ///   - iconOffName: Optional asset name for the off-state icon
    public init(index: Int, glyphText: String, accentColor: NSColor,
                iconOnName: String? = nil, iconOffName: String? = nil) {
        self.index = index
        self.accentColor = accentColor
        self.badge = CodexMetalBadge(diameter: AudioToggleButton.buttonSize)
        super.init(frame: NSRect(x: 0, y: 0,
                                 width: AudioToggleButton.buttonSize,
                                 height: AudioToggleButton.buttonSize))
        wantsLayer = true
        layer?.masksToBounds = false  // Shadow extends beyond button bounds

        // Configure badge
        badge.glyphText = glyphText
        badge.frame = bounds
        badge.layer?.borderWidth = 2.5  // Override default 1pt stroke width

        // Load icons via GhostFabResources (handles both compiled xcassets and raw PNGs)
        iconOn = Self.loadIcon(named: iconOnName)
        iconOff = Self.loadIcon(named: iconOffName)

        addSubview(badge)

        // Apply initial inactive state
        applyToggleState()
    }

    private static func loadIcon(named name: String?) -> NSImage? {
        guard let name = name else { return nil }
        return GhostFabResources.image(named: name)
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    // MARK: - State Application

    /// Updates badge glyph color, stroke color, glow, and shadow to match current toggle state.
    public func applyToggleState() {
        // Swap icon for on/off state (before color changes trigger applyState)
        badge.iconImage = isOn ? iconOn : iconOff

        if isOn {
            // Active: accent colors, glow visible, accent shadow
            badge.glyphColor = accentColor
            badge.strokeColor = accentColor
            badge.showGlow = true
            badge.glowColor = accentColor

            // Active shadow: accent glow centered
            layer?.shadowColor = accentColor.cgColor
            layer?.shadowOffset = CGSize(width: 0, height: 0)
            layer?.shadowRadius = 10
            layer?.shadowOpacity = 0.5
        } else {
            // Inactive: muted colors, no glow, dark offset shadow
            badge.glyphColor = AudioToggleButton.mutedColor
            badge.strokeColor = AudioToggleButton.mutedColor
            badge.showGlow = false

            // Inactive shadow: black, offset downward
            layer?.shadowColor = NSColor.black.cgColor
            layer?.shadowOffset = CGSize(width: 0, height: 2)
            layer?.shadowRadius = 4
            layer?.shadowOpacity = 0.5
        }

        // Shadow path for correct rendering (circle)
        let shadowPath = CGPath(ellipseIn: bounds, transform: nil)
        layer?.shadowPath = shadowPath
    }

    // MARK: - Hit Testing

    public override func mouseDown(with event: NSEvent) {
        let center = CGPoint(x: bounds.midX, y: bounds.midY)
        let clickPoint = convert(event.locationInWindow, from: nil)
        let dx = clickPoint.x - center.x
        let dy = clickPoint.y - center.y
        let distance = sqrt(dx * dx + dy * dy)

        // Circular hit test: 43pt diameter -> 21.5pt radius
        if distance <= AudioToggleButton.buttonSize / 2 {
            isOn.toggle()
            audioToggleCallback?(Int32(index), isOn)
        } else {
            super.mouseDown(with: event)
        }
    }
}
