// swift-tools-version: 5.9
import PackageDescription
let package = Package(name: "LGInputSwitch", platforms: [.macOS(.v13)], products: [
    .executable(name: "LGInputSwitch", targets: ["LGInputSwitch"])
], targets: [
    .target(name: "InputSwitchCore"),
    .executableTarget(name: "LGInputSwitch", dependencies: ["InputSwitchCore"], linkerSettings: [.linkedFramework("Carbon"), .linkedFramework("ServiceManagement")]),
    .testTarget(name: "InputSwitchCoreTests", dependencies: ["InputSwitchCore"])
])
