//
//  GhostFabTokens.swift
//  GhostFab-AppKit
//
//  Visual design tokens translated from GhostFabCodexTokens.cs
//  Source of truth: DesignReference/GhostFabCodex/Core/GhostFabCodexTokens.cs
//

import AppKit
import QuartzCore

// MARK: - Private Color Helpers (file-scope to avoid Swift type-checker issues)

/// Creates an NSColor from 0-255 integer RGB values in sRGB color space.
private func _color(_ r: Int, _ g: Int, _ b: Int, _ a: CGFloat = 1.0) -> NSColor {
    let rf: CGFloat = CGFloat(r) / 255.0
    let gf: CGFloat = CGFloat(g) / 255.0
    let bf: CGFloat = CGFloat(b) / 255.0
    return NSColor(srgbRed: rf, green: gf, blue: bf, alpha: a)
}

/// Creates an NSColor from 0-255 integer RGBA values in sRGB color space.
private func _rgba(_ r: Int, _ g: Int, _ b: Int, _ a: Int) -> NSColor {
    let rf: CGFloat = CGFloat(r) / 255.0
    let gf: CGFloat = CGFloat(g) / 255.0
    let bf: CGFloat = CGFloat(b) / 255.0
    let af: CGFloat = CGFloat(a) / 255.0
    return NSColor(srgbRed: rf, green: gf, blue: bf, alpha: af)
}

// MARK: - GhostFabTokens

/// Single source of truth for the gunmetal Codex palette in Swift.
/// Enum prevents instantiation -- access everything via static members.
public enum GhostFabTokens {

    // MARK: - Sizing Constants

    public static let FabSize: CGFloat = 110
    public static let FabRingPadding: CGFloat = 14
    public static let SmallBadgeSize: CGFloat = 36
    public static let BarHeight: CGFloat = 72
    public static let BarCornerRadius: CGFloat = 8
    public static let CaseCornerRadius: CGFloat = 16
    public static let ToolbarHeight: CGFloat = 100
    public static let SeparatorThickness: CGFloat = 2
    public static let CaseWidth: CGFloat = 630
    public static let BadgeInset: CGFloat = 8
    public static let SpineCornerRadius: CGFloat = 12
    public static let SpineInset: CGFloat = 20

    // MARK: - Color Palette

    /// #0A0A10 -- deepest background
    public static let DeepSpace = _color(0x0A, 0x0A, 0x10)

    /// #14141E -- dark background
    public static let Midnight = _color(0x14, 0x14, 0x1E)

    /// #1E1E2E -- navy background
    public static let Navy = _color(0x1E, 0x1E, 0x2E)

    /// #2E2E3E -- dark steel
    public static let SteelDark = _color(0x2E, 0x2E, 0x3E)

    /// #58586A -- mid steel
    public static let SteelMid = _color(0x58, 0x58, 0x6A)

    /// #8A8A9C -- light steel
    public static let SteelLight = _color(0x8A, 0x8A, 0x9C)

    /// #484858 -- ghost blue accent
    public static let GhostBlue = _color(0x48, 0x48, 0x58)

    /// #22C55E -- voice activity indicator
    public static let VoiceGreen = _color(0x22, 0xC5, 0x5E)

    /// #4EA3FF -- voice command indicator
    public static let VoiceCommandBlue = _color(0x4E, 0xA3, 0xFF)

    /// #F2A900 -- game audio indicator
    public static let GameAudioAmber = _color(0xF2, 0xA9, 0x00)

    /// #E11D48 -- audio-in indicator
    public static let AudioInRose = _color(0xE1, 0x1D, 0x48)

    /// #EF4444 -- microphone indicator
    public static let MicRed = _color(0xEF, 0x44, 0x44)

    // MARK: - Gradient Layer Factories

    /// Radial gradient from center for the ghost FAB fill.
    /// Colors: #505060 -> #2E2E3E -> Navy
    public static func makeGhostFillGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.type = .radial
        layer.startPoint = CGPoint(x: 0.5, y: 0.5)
        layer.endPoint = CGPoint(x: 1.0, y: 1.0)
        layer.colors = [
            _color(0x50, 0x50, 0x60).cgColor,
            _color(0x2E, 0x2E, 0x3E).cgColor,
            Navy.cgColor
        ]
        layer.locations = [0, 0.55, 1.0]
        return layer
    }

    /// Vertical linear gradient for the steel ring.
    /// Colors: SteelDark -> SteelMid -> SteelLight -> SteelMid -> SteelDark
    public static func makeSteelRingGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.startPoint = CGPoint(x: 0.5, y: 0)
        layer.endPoint = CGPoint(x: 0.5, y: 1)
        layer.colors = [
            SteelDark.cgColor,
            SteelMid.cgColor,
            SteelLight.cgColor,
            SteelMid.cgColor,
            SteelDark.cgColor
        ]
        layer.locations = [0, 0.22, 0.5, 0.78, 1.0]
        return layer
    }

    /// Vertical linear gradient for the case background.
    /// Colors: semi-transparent SteelDark / Navy / SteelDark
    public static func makeCaseGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.startPoint = CGPoint(x: 0.5, y: 0)
        layer.endPoint = CGPoint(x: 0.5, y: 1)
        layer.colors = [
            _rgba(0x2E, 0x2E, 0x3E, 0x1A).cgColor,
            _rgba(0x1E, 0x1E, 0x2E, 0x33).cgColor,
            _rgba(0x2E, 0x2E, 0x3E, 0x1A).cgColor
        ]
        layer.locations = [0, 0.5, 1.0]
        return layer
    }

    /// Vertical linear gradient for the bar.
    /// Colors: SteelDark -> SteelMid -> SteelLight -> SteelMid -> SteelDark
    public static func makeBarGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.startPoint = CGPoint(x: 0.5, y: 0)
        layer.endPoint = CGPoint(x: 0.5, y: 1)
        layer.colors = [
            SteelDark.cgColor,
            SteelMid.cgColor,
            SteelLight.cgColor,
            SteelMid.cgColor,
            SteelDark.cgColor
        ]
        layer.locations = [0, 0.3, 0.5, 0.7, 1.0]
        return layer
    }

    /// Vertical linear gradient for top highlight effect.
    /// Colors: white@20% -> white@0%
    public static func makeHighlightGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.startPoint = CGPoint(x: 0.5, y: 0)
        layer.endPoint = CGPoint(x: 0.5, y: 1)
        layer.colors = [
            _rgba(0xFF, 0xFF, 0xFF, 0x33).cgColor,
            _rgba(0xFF, 0xFF, 0xFF, 0x00).cgColor
        ]
        layer.locations = [0, 0.58]
        return layer
    }

    /// Vertical linear gradient for bottom highlight effect.
    /// Colors: white@0% -> white@20% -> white@0%
    public static func makeHighlightBottomGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.startPoint = CGPoint(x: 0.5, y: 0)
        layer.endPoint = CGPoint(x: 0.5, y: 1)
        layer.colors = [
            _rgba(0xFF, 0xFF, 0xFF, 0x00).cgColor,
            _rgba(0xFF, 0xFF, 0xFF, 0x33).cgColor,
            _rgba(0xFF, 0xFF, 0xFF, 0x00).cgColor
        ]
        layer.locations = [0.4, 0.8, 1.0]
        return layer
    }

    /// Vertical linear gradient for the spine panel.
    /// Colors: Navy@40% -> SteelDark@25% -> Navy@35%
    public static func makeSpineGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.startPoint = CGPoint(x: 0.5, y: 0)
        layer.endPoint = CGPoint(x: 0.5, y: 1)
        layer.colors = [
            _rgba(0x1E, 0x1E, 0x2E, 0x66).cgColor,
            _rgba(0x2E, 0x2E, 0x3E, 0x40).cgColor,
            _rgba(0x1E, 0x1E, 0x2E, 0x59).cgColor
        ]
        layer.locations = [0, 0.52, 1.0]
        return layer
    }

    /// Radial gradient from top-center for spine highlight.
    /// Colors: white@8% -> clear
    public static func makeSpineRadialHighlightGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.type = .radial
        layer.startPoint = CGPoint(x: 0.5, y: 0)
        layer.endPoint = CGPoint(x: 1.0, y: 1.0)
        layer.colors = [
            _rgba(0xFF, 0xFF, 0xFF, 0x14).cgColor,
            NSColor.clear.cgColor
        ]
        layer.locations = [0, 1.0]
        return layer
    }

    /// Diagonal linear gradient for spine stroke.
    /// Colors: white@30% -> white@8% -> white@15%
    public static func makeSpineStrokeGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.startPoint = CGPoint(x: 0, y: 0)
        layer.endPoint = CGPoint(x: 1, y: 1)
        layer.colors = [
            _rgba(0xFF, 0xFF, 0xFF, 0x4D).cgColor,
            _rgba(0xFF, 0xFF, 0xFF, 0x14).cgColor,
            _rgba(0xFF, 0xFF, 0xFF, 0x26).cgColor
        ]
        layer.locations = [0, 0.5, 1.0]
        return layer
    }

    /// Radial gradient from center for ghost shadow.
    /// Colors: gray@33% -> gray@13% -> clear
    public static func makeGhostShadowGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.type = .radial
        layer.startPoint = CGPoint(x: 0.5, y: 0.5)
        layer.endPoint = CGPoint(x: 1.0, y: 1.0)
        layer.colors = [
            _rgba(0x80, 0x80, 0x90, 0x55).cgColor,
            _rgba(0x80, 0x80, 0x90, 0x22).cgColor,
            NSColor.clear.cgColor
        ]
        layer.locations = [0, 0.6, 1.0]
        return layer
    }

    /// Solid Navy at ~94% alpha for toolbar background.
    /// Uses CAGradientLayer for API consistency with other token gradients.
    public static func makeToolbarGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        let toolbarColor = _rgba(0x1E, 0x1E, 0x2E, 0xF0).cgColor
        layer.colors = [toolbarColor, toolbarColor]
        layer.locations = [0, 1]
        return layer
    }

    /// Horizontal linear gradient for separator line.
    /// Colors: clear -> white@30% -> SteelLight -> white@30% -> clear
    public static func makeSeparatorGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.startPoint = CGPoint(x: 0, y: 0.5)
        layer.endPoint = CGPoint(x: 1, y: 0.5)
        layer.colors = [
            NSColor.clear.cgColor,
            _rgba(0xFF, 0xFF, 0xFF, 0x4D).cgColor,
            SteelLight.cgColor,
            _rgba(0xFF, 0xFF, 0xFF, 0x4D).cgColor,
            NSColor.clear.cgColor
        ]
        layer.locations = [0, 0.15, 0.5, 0.85, 1.0]
        return layer
    }

    /// Diagonal linear gradient for case stroke.
    /// Colors: white@30% -> white@8% -> white@15%
    public static func makeCaseStrokeGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.startPoint = CGPoint(x: 0, y: 0)
        layer.endPoint = CGPoint(x: 1, y: 1)
        layer.colors = [
            _rgba(0xFF, 0xFF, 0xFF, 0x4D).cgColor,
            _rgba(0xFF, 0xFF, 0xFF, 0x14).cgColor,
            _rgba(0xFF, 0xFF, 0xFF, 0x26).cgColor
        ]
        layer.locations = [0, 0.5, 1.0]
        return layer
    }

    /// Solid white@25% for FAB core stroke.
    /// Uses CAGradientLayer for API consistency with other token gradients.
    public static func makeFabCoreStrokeGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        let strokeColor = _rgba(0xFF, 0xFF, 0xFF, 0x40).cgColor
        layer.colors = [strokeColor, strokeColor]
        layer.locations = [0, 1]
        return layer
    }

    /// Vertical linear gradient for badge highlight.
    /// Colors: white@25% -> white@0%
    public static func makeBadgeHighlightGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        layer.startPoint = CGPoint(x: 0.5, y: 0)
        layer.endPoint = CGPoint(x: 0.5, y: 1)
        layer.colors = [
            _rgba(0xFF, 0xFF, 0xFF, 0x40).cgColor,
            _rgba(0xFF, 0xFF, 0xFF, 0x00).cgColor
        ]
        layer.locations = [0, 0.55]
        return layer
    }

    /// Solid white@15% for badge stroke.
    /// Uses CAGradientLayer for API consistency with other token gradients.
    public static func makeBadgeStrokeGradient() -> CAGradientLayer {
        let layer = CAGradientLayer()
        let strokeColor = _rgba(0xFF, 0xFF, 0xFF, 0x26).cgColor
        layer.colors = [strokeColor, strokeColor]
        layer.locations = [0, 1]
        return layer
    }
}
