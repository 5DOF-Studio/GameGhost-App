// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "GaimerSpeech",
    platforms: [
        .macCatalyst(.v16),
        .macOS(.v14)
    ],
    products: [
        .library(
            name: "GaimerSpeech",
            type: .dynamic,
            targets: ["GaimerSpeech"]
        )
    ],
    targets: [
        .target(
            name: "GaimerSpeech",
            path: "Sources/GaimerSpeech",
            linkerSettings: [
                .linkedFramework("Speech"),
                .linkedFramework("AVFoundation"),
                .linkedFramework("AVFAudio")
            ]
        )
    ]
)
