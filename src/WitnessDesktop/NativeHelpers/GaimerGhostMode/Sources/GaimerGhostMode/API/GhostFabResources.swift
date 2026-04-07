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
    /// Falls back to loading PNGs directly from uncompiled xcassets
    /// (needed when running via `swift run` where actool doesn't compile the catalog).
    public static func image(named name: String) -> NSImage? {
        // Try compiled asset catalog first (framework / xcodebuild)
        if let img = bundle.image(forResource: name) {
            return img
        }

        // Fallback: SwiftPM copies raw xcassets — look for PNG inside imageset dirs
        let candidates = [bundle.resourceURL, bundle.bundleURL]
        for base in candidates.compactMap({ $0 }) {
            let imagesetDir = base.appendingPathComponent("Assets.xcassets")
                .appendingPathComponent("\(name).imageset")

            // Try exact match first: {name}.png
            let exactURL = imagesetDir.appendingPathComponent("\(name).png")
            if let img = NSImage(contentsOf: exactURL) { return img }

            // Imageset name may differ from PNG filename — find any .png in the dir
            if let contents = try? FileManager.default.contentsOfDirectory(
                at: imagesetDir, includingPropertiesForKeys: nil
            ) {
                for url in contents where url.pathExtension == "png" {
                    if let img = NSImage(contentsOf: url) { return img }
                }
            }
        }

        return nil
    }
}
