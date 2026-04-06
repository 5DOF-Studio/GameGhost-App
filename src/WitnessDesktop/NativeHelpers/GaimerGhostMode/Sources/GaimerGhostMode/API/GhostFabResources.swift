//
//  GhostFabResources.swift
//  GhostFab-AppKit
//
//  Public accessor for bundled assets. In SwiftPM, Bundle.module resolves to the
//  package resource bundle; in Xcode/xcframework, Bundle(for:) resolves to the
//  framework bundle. External consumers (harness, tests) use this instead of
//  guessing the bundle path.
//

import AppKit

public enum GhostFabResources {
    private static let resourceBundleName = "GaimerGhostMode_GaimerGhostMode"

    /// The bundle containing GhostFab-AppKit's compiled asset catalog.
    public static var bundle: Bundle {
        let frameworkBundle = Bundle(for: GhostContentView.self)
        let candidates: [URL?] = [
            frameworkBundle.resourceURL,
            frameworkBundle.bundleURL,
            Bundle.main.resourceURL,
            Bundle.main.bundleURL
        ]

        for candidate in candidates {
            guard let candidate else { continue }
            let bundleURL = candidate.appendingPathComponent(resourceBundleName + ".bundle")
            if let resourceBundle = Bundle(url: bundleURL) {
                return resourceBundle
            }
        }

        return frameworkBundle
    }

    /// Loads a named image from the library's asset catalog.
    public static func image(named name: String) -> NSImage? {
        bundle.image(forResource: name)
    }
}
