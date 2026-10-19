import XCTest
@testable import InputSwitchCore
final class CoreTests: XCTestCase {
    func testMonitorMustBeExplicitAndValid() throws {
        var c = Config()
        XCTAssertThrowsError(try c.validate())
        c.monitor = "12345678-1234-1234-1234-123456789ABC"
        try c.validate()
        XCTAssertEqual(c.arguments, ["display", c.monitor, "set", "input-alt", "144"])
        c.profile = "standard"; c.value = 17
        XCTAssertEqual(c.arguments.suffix(2), ["input", "17"])
        c.value = 65536; XCTAssertThrowsError(try c.validate())
    }
    func testHotkeyRejections() throws {
        XCTAssertEqual(try Hotkey("Ctrl+Alt+S").key, "S")
        for bad in ["S", "Ctrl++S", "Ctrl+Ctrl+S", "Ctrl+F12", "Shift+S", "Ctrl+🍎", "Win+S"] {
            XCTAssertThrowsError(try Hotkey(bad), bad)
        }
    }
    func testNoFallbackToFirstMonitor() {
        let id = "12345678-1234-1234-1234-123456789ABC"
        let found = Display.parse("[1] LG ULTRAFINE (\(id))\n[2] Other (invalid)\n")
        XCTAssertEqual(found.count, 1); XCTAssertEqual(found.first?.uuid, id)
        XCTAssertTrue(Display.parse("No external display found").isEmpty)
    }
    func testCooldownIncludesFailuresAndBoundary() {
        var g = SwitchGate()
        XCTAssertTrue(g.begin(now: 0)); XCTAssertFalse(g.begin(now: 0))
        XCTAssertFalse(g.begin(now: 4.999)); XCTAssertTrue(g.begin(now: 5))
    }
}
