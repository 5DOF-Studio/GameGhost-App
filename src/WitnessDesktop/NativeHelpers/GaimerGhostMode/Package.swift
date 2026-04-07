// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "GaimerGhostMode",
    platforms: [
        .macOS(.v13)
    ],
    products: [
        .library(
            name: "GaimerGhostMode",
            type: .dynamic,
            targets: ["GaimerGhostMode"]
        )
    ],
    targets: [
        .target(
            name: "GaimerGhostMode",
            path: "Sources/GaimerGhostMode",
            resources: [
                .process("Assets.xcassets")
            ],
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("CoreGraphics")
            ]
        ),
        .executableTarget(
            name: "GhostModeHarness",
            dependencies: ["GaimerGhostMode"],
            path: "Harness",
            resources: [
                .copy("Resources")
            ]
        ),
    ]
)
