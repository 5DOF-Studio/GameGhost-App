// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "GaimerScreenCapture",
    platforms: [
        .macCatalyst(.v16),
        .macOS(.v14)
    ],
    products: [
        .library(
            name: "GaimerScreenCapture",
            type: .dynamic,
            targets: ["GaimerScreenCapture"]
        )
    ],
    targets: [
        .target(
            name: "GaimerScreenCapture",
            path: "Sources/GaimerScreenCapture",
            linkerSettings: [
                .linkedFramework("ScreenCaptureKit"),
                .linkedFramework("CoreGraphics"),
                .linkedFramework("CoreImage"),
                .linkedFramework("UIKit", .when(platforms: [.macCatalyst]))
            ]
        )
    ]
)
