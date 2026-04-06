// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "GaimerViews",
    platforms: [
        .macCatalyst(.v16)
    ],
    products: [
        .library(
            name: "GaimerViews",
            type: .dynamic,
            targets: ["GaimerViews"]
        )
    ],
    targets: [
        .target(
            name: "GaimerViews",
            path: "Sources/GaimerViews",
            linkerSettings: [
                .linkedFramework("SwiftUI"),
                .linkedFramework("UIKit", .when(platforms: [.macCatalyst]))
            ]
        )
    ]
)
