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
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("CoreGraphics")
            ]
        )
    ]
)
