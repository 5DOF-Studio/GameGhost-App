//
//  VadMeterView.swift
//  GhostFab-AppKit
//
//  12-bar Voice Activity Detection meter with green/yellow/red color coding.
//  Bars light proportionally based on level (0.0 = silence, 1.0 = max).
//  Source of truth: DesignReference/GhostFabCodex spec VAD-01
//

import AppKit
import QuartzCore

public class VadMeterView: NSView {

    // MARK: - Constants

    private static let barCount = 12
    private static let barWidth: CGFloat = 5
    private static let barHeight: CGFloat = 20
    private static let barSpacing: CGFloat = 3
    private static let barCornerRadius: CGFloat = 2

    /// Inactive bar color: #1A1225
    private static let inactiveColor = NSColor(srgbRed: 0x1A / 255.0,
                                                green: 0x12 / 255.0,
                                                 blue: 0x25 / 255.0,
                                                alpha: 1.0)

    // MARK: - State

    private var barLayers: [CALayer] = []

    // MARK: - Initialization

    override public init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        wantsLayer = true
        setupBars()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    // MARK: - Setup

    private func setupBars() {
        guard let layer = self.layer else { return }

        for _ in 0..<VadMeterView.barCount {
            let bar = CALayer()
            bar.cornerRadius = VadMeterView.barCornerRadius
            bar.backgroundColor = VadMeterView.inactiveColor.cgColor
            layer.addSublayer(bar)
            barLayers.append(bar)
        }
    }

    // MARK: - Layout

    override public func layout() {
        super.layout()

        let stride = VadMeterView.barWidth + VadMeterView.barSpacing  // 8pt
        let y = bounds.height - VadMeterView.barHeight  // bottom-align within bounds

        for (i, bar) in barLayers.enumerated() {
            bar.frame = CGRect(x: CGFloat(i) * stride,
                               y: y,
                               width: VadMeterView.barWidth,
                               height: VadMeterView.barHeight)
        }
    }

    // MARK: - Level API

    /// Sets the VAD level, lighting bars proportionally.
    /// - Parameter level: 0.0 (silence) to 1.0 (max). Clamped.
    public func setLevel(_ level: CGFloat) {
        let clamped = min(max(level, 0), 1)
        let litCount = Int(round(clamped * CGFloat(VadMeterView.barCount)))

        for i in 0..<VadMeterView.barCount {
            if i < litCount {
                barLayers[i].backgroundColor = colorForBar(i).cgColor
            } else {
                barLayers[i].backgroundColor = VadMeterView.inactiveColor.cgColor
            }
        }
    }

    // MARK: - Color Helpers

    /// Returns the active color for a bar based on its index.
    /// Bars 0-7: green, bars 8-10: yellow/amber, bar 11: red.
    private func colorForBar(_ index: Int) -> NSColor {
        switch index {
        case 0...7:
            return GhostFabTokens.VoiceGreen      // #22C55E
        case 8...10:
            return GhostFabTokens.GameAudioAmber   // #F2A900
        case 11:
            return GhostFabTokens.MicRed           // #EF4444
        default:
            return VadMeterView.inactiveColor
        }
    }
}
