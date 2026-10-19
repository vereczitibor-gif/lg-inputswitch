import Foundation

public enum SwitchError: LocalizedError {
    case message(String)
    public var errorDescription: String? { if case .message(let s) = self { return s }; return nil }
}

public struct Config: Codable, Equatable {
    public var monitor = ""
    public var profile = "lg"
    public var value = 144
    public var hotkey = "Ctrl+Alt+S"
    public init() {}
    public func validate() throws {
        guard UUID(uuidString: monitor) != nil else { throw SwitchError.message("Válassz ki egy monitort a Beállításokban.") }
        guard ["lg", "standard"].contains(profile), (0...65535).contains(value) else { throw SwitchError.message("Érvénytelen profil vagy bemenetkód (0–65535).") }
        _ = try Hotkey(hotkey)
    }
    public var arguments: [String] { ["display", monitor, "set", profile == "lg" ? "input-alt" : "input", String(value)] }
}

public struct Hotkey: Equatable {
    public let key: String
    public let modifiers: Set<String>
    public init(_ value: String) throws {
        let parts = value.uppercased().split(separator: "+", omittingEmptySubsequences: false).map { $0.trimmingCharacters(in: .whitespaces) }
        let keys = Array("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789").map(String.init) + (1...11).map { "F\($0)" }
        guard parts.count >= 2, let last = parts.last, keys.contains(last) else { throw SwitchError.message("Hotkey: pl. Ctrl+Alt+S; A–Z, 0–9, F1–F11 támogatott.") }
        let mods = Array(parts.dropLast())
        guard Set(mods).count == mods.count, mods.allSatisfy({ ["CTRL", "ALT", "SHIFT", "CMD"].contains($0) }), mods.contains("CTRL") || mods.contains("ALT") || mods.contains("CMD") else { throw SwitchError.message("Használj Ctrl, Alt, Shift, Cmd módosítókat; legalább Ctrl, Alt vagy Cmd szükséges.") }
        key = last; modifiers = Set(mods)
    }
}

public struct Display: Equatable {
    public let name: String
    public let uuid: String
    public static func parse(_ text: String) -> [Display] {
        let regex = try! NSRegularExpression(pattern: #"^\[\d+\]\s+(.+?)\s+\(([0-9a-fA-F-]{36})\)\s*$"#, options: .anchorsMatchLines)
        return regex.matches(in: text, range: NSRange(text.startIndex..., in: text)).compactMap { m in
            guard let n = Range(m.range(at: 1), in: text), let u = Range(m.range(at: 2), in: text), UUID(uuidString: String(text[u])) != nil else { return nil }
            return Display(name: String(text[n]), uuid: String(text[u]))
        }
    }
}

/// Monotonic clock supplied by caller; reject rapid taps and concurrent writes.
public struct SwitchGate {
    private var until: TimeInterval = -.infinity
    public init() {}
    public mutating func begin(now: TimeInterval) -> Bool {
        guard now >= until else { return false }
        until = now + 5
        return true
    }
}
