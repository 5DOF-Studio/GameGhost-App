//
//  GhostFabMetrics.swift
//  GhostFab-AppKit
//
//  Shared layout contracts for the AppKit panel and future tests.
//

import CoreGraphics
import Foundation

public enum GhostFabMetrics {
    public static var collapsedPanelSize: CGSize {
        CGSize(
            width: GhostFabTokens.CaseWidth,
            height: GhostFabTokens.BarHeight * 2 + GhostFabTokens.SeparatorThickness
        )
    }

    public static func panelHeight(forSpineHeight spineHeight: CGFloat) -> CGFloat {
        let negativeMargins: CGFloat = 12
        let spineContribution = max(0, spineHeight - negativeMargins)
        return (GhostFabTokens.BarHeight * 2) + GhostFabTokens.SeparatorThickness + spineContribution
    }

    public static func panelSize(forSpineHeight spineHeight: CGFloat) -> CGSize {
        CGSize(width: GhostFabTokens.CaseWidth, height: panelHeight(forSpineHeight: spineHeight))
    }

    public static func defaultTopRightOrigin(in visibleFrame: CGRect, panelSize: CGSize, padding: CGFloat) -> CGPoint {
        CGPoint(
            x: visibleFrame.maxX - panelSize.width - padding,
            y: visibleFrame.maxY - panelSize.height - padding
        )
    }
}
