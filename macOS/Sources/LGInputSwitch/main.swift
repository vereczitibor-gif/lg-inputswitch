import AppKit
import Carbon
import ServiceManagement
import InputSwitchCore
import Darwin

enum Storage {
    static let folder = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("LGInputSwitch")
    static let config = folder.appendingPathComponent("config.json")
    static let log = folder.appendingPathComponent("diagnostics.log")
    static func save(_ value: Config) throws {
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let encoder = JSONEncoder(); encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try encoder.encode(value).write(to: config, options: .atomic)
    }
    static func record(_ text: String) {
        // Called only on main thread. Bounded local log; no network traffic.
        try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        var data = (try? Data(contentsOf: log)) ?? Data()
        if data.count > 262144 { data = Data(data.suffix(131072)) }
        data.append(Data("\(ISO8601DateFormatter().string(from: Date())) \(text)\n".utf8))
        try? data.write(to: log, options: .atomic)
    }
}

enum Backend {
    static var helper: URL { Bundle.main.bundleURL.appendingPathComponent("Contents/Helpers/m1ddc") }
    static func run(_ args: [String]) throws -> String {
        let p = Process(), pipe = Pipe()
        p.executableURL = helper; p.arguments = args
        p.standardOutput = pipe; p.standardError = pipe
        try p.run()
        let timer = DispatchSource.makeTimerSource(queue: DispatchQueue.global())
        timer.schedule(deadline: .now() + 8)
        timer.setEventHandler { if p.isRunning { kill(p.processIdentifier, SIGKILL) } }
        timer.resume()
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        p.waitUntilExit(); timer.cancel()
        let output = String(decoding: data, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines)
        guard p.terminationStatus == 0 else { throw SwitchError.message("DDC/helper hiba (\(p.terminationStatus)): \(output). Ellenőrizd a kábelt, a DDC/CI-t és a monitorválasztást; időtúllépésnél a váltás eredménye ismeretlen.") }
        return output
    }
}

final class App: NSObject, NSApplicationDelegate {
    var config = Config()
    var item: NSStatusItem!
    var status = "Váltás még nem történt; aktív bemenet ismeretlen."
    var hotkey: EventHotKeyRef?
    var handler: EventHandlerRef?
    var hotkeyID: UInt32 = 1
    var gate = SwitchGate()
    var busy = false
    var hotkeyDown = false
    var settings: NSWindow?
    var diagnostic: NSWindow?
    var displays: [Display] = []
    let monitor = NSPopUpButton(), profile = NSPopUpButton(), preset = NSPopUpButton()
    let value = NSTextField(), shortcut = NSTextField(), hint = NSTextField(wrappingLabelWithString: "")
    let login = NSButton(checkboxWithTitle: "Indítás bejelentkezéskor", target: nil, action: nil)

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        item.button?.image = NSImage(systemSymbolName: "display.2", accessibilityDescription: "LG InputSwitch")
        if FileManager.default.fileExists(atPath: Storage.config.path) {
            do { config = try JSONDecoder().decode(Config.self, from: Data(contentsOf: Storage.config)); try config.validate() }
            catch { status = "Konfigurációs hiba: \(error.localizedDescription)"; config = Config() }
        }
        var specs = [EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed)), EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyReleased))]
        let result = InstallEventHandler(GetApplicationEventTarget(), { _, event, context in
            guard let context, let event else { return OSStatus(eventNotHandledErr) }
            let app = Unmanaged<App>.fromOpaque(context).takeUnretainedValue()
            if GetEventKind(event) == UInt32(kEventHotKeyReleased) { app.hotkeyDown = false }
            else if !app.hotkeyDown { app.hotkeyDown = true; app.switchInput() }
            return noErr
        }, 2, &specs, Unmanaged.passUnretained(self).toOpaque(), &handler)
        if result == noErr {
            do { try register(config.hotkey) } catch { status = error.localizedDescription }
        } else { status = "Globális hotkey eseménykezelő hiba: \(result)" }
        Storage.record("Indulás: \(status)"); rebuildMenu()
        NSWorkspace.shared.notificationCenter.addObserver(self, selector: #selector(woke), name: NSWorkspace.didWakeNotification, object: nil)
        // No monitor write and no login registration at launch.
        if config.monitor.isEmpty { showSettings() }
    }
    @objc func woke() { hotkeyDown = false; setStatus("Felébredés: a következő váltás frissen felderíti a kiválasztott monitort.") }
    func register(_ text: String) throws {
        let parsed = try Hotkey(text)
        let names = ["A":0,"S":1,"D":2,"F":3,"H":4,"G":5,"Z":6,"X":7,"C":8,"V":9,"B":11,"Q":12,"W":13,"E":14,"R":15,"Y":16,"T":17,"1":18,"2":19,"3":20,"4":21,"6":22,"5":23,"9":25,"7":26,"8":28,"0":29,"O":31,"U":32,"I":34,"P":35,"L":37,"J":38,"K":40,"N":45,"M":46,"F1":122,"F2":120,"F3":99,"F4":118,"F5":96,"F6":97,"F7":98,"F8":100,"F9":101,"F10":109,"F11":103]
        var modifiers: UInt32 = 0
        for (name, mask) in [("CTRL", controlKey), ("ALT", optionKey), ("SHIFT", shiftKey), ("CMD", cmdKey)] where parsed.modifiers.contains(name) { modifiers |= UInt32(mask) }
        var candidate: EventHotKeyRef?
        let next = hotkeyID == 1 ? UInt32(2) : 1
        let rc = RegisterEventHotKey(UInt32(names[parsed.key]!), modifiers, EventHotKeyID(signature: 0x4C474953, id: next), GetApplicationEventTarget(), 0, &candidate)
        guard rc == noErr else { throw SwitchError.message("A hotkey nem regisztrálható (\(rc)); lehet, hogy foglalt.") }
        if let hotkey { UnregisterEventHotKey(hotkey) }
        hotkey = candidate; hotkeyID = next; hotkeyDown = false
    }
    func rebuildMenu() {
        let menu = NSMenu()
        let title = NSMenuItem(title: "Váltási kérés → \(config.value)  [\(config.hotkey)]", action: #selector(switchInput), keyEquivalent: "")
        title.target = self; title.isEnabled = !busy && !config.monitor.isEmpty; menu.addItem(title)
        let state = NSMenuItem(title: String(status.prefix(110)), action: nil, keyEquivalent: ""); state.isEnabled = false; menu.addItem(state)
        menu.addItem(.separator())
        for (label, action) in [("Beállítások…", #selector(showSettings)), ("Diagnosztika (csak olvasás)…", #selector(showDiagnostics)), ("Kilépés", #selector(quit))] {
            let entry = NSMenuItem(title: label, action: action, keyEquivalent: ""); entry.target = self; menu.addItem(entry)
        }
        item.menu = menu; item.button?.toolTip = status
    }
    func setStatus(_ text: String) { status = text; Storage.record(text); rebuildMenu(); hint.stringValue = text }
    @objc func switchInput() {
        do { try config.validate() } catch { setStatus(error.localizedDescription); showSettings(); return }
        guard !busy, gate.begin(now: ProcessInfo.processInfo.systemUptime) else { return }
        busy = true; setStatus("Váltási kérés küldése…")
        let snapshot = config
        DispatchQueue.global(qos: .userInitiated).async {
            let result: String
            do {
                let list = try Backend.run(["display", "list"])
                guard Display.parse(list).filter({ $0.uuid == snapshot.monitor }).count == 1 else { throw SwitchError.message("A kiválasztott monitor nem érhető el. Nincs automatikus helyettesítés; frissítsd a listát.") }
                let output = try Backend.run(snapshot.arguments)
                result = "Parancs elküldve (\(snapshot.profile), \(snapshot.value)); a fizikai váltás NINCS igazolva. \(output)"
            } catch { result = error.localizedDescription }
            DispatchQueue.main.async { self.busy = false; self.setStatus(result) }
        }
    }
    @objc func showSettings() {
        if let settings { settings.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true); return }
        let w = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 640, height: 465), styleMask: [.titled, .closable], backing: .buffered, defer: false)
        w.title = "LG InputSwitch – Beállítások"; w.isReleasedWhenClosed = false; settings = w
        let root = w.contentView!
        func row(_ label: String, _ control: NSView, _ y: CGFloat) {
            let text = NSTextField(labelWithString: label); text.frame = NSRect(x: 20, y: y+3, width: 160, height: 22); root.addSubview(text)
            control.frame = NSRect(x: 185, y: y, width: 430, height: 28); root.addSubview(control)
        }
        row("Monitor", monitor, 405)
        profile.addItems(withTitles: ["LG 32U990A-S / 0xF4 (kódok ellenőrizendők)", "Szabványos VCP 0x60"])
        profile.selectItem(at: config.profile == "lg" ? 0 : 1); profile.target = self; profile.action = #selector(usePreset)
        row("Protokoll", profile, 355)
        preset.addItems(withTitles: ["HDMI 1", "Thunderbolt / USB-C", "DisplayPort 1", "Egyedi kód"]); preset.selectItem(at: 3)
        preset.target = self; preset.action = #selector(usePreset); row("Célbemenet", preset, 305)
        value.stringValue = String(config.value); row("Bemenetkód (dec.)", value, 255)
        shortcut.stringValue = config.hotkey; row("Globális hotkey", shortcut, 205)
        login.frame = NSRect(x: 185, y: 160, width: 420, height: 26); login.target = self; login.action = #selector(toggleLogin)
        login.state = SMAppService.mainApp.status == .enabled ? .on : .off; root.addSubview(login)
        hint.stringValue = "A mentés nem vált bemenetet. Mac: Ctrl = Control, Alt = Option. Az indítási opció külön, azonnal alkalmazódik."
        hint.frame = NSRect(x: 20, y: 65, width: 595, height: 80); root.addSubview(hint)
        for (text, x, action) in [("Lista frissítése", CGFloat(20), #selector(refresh)), ("Váltási kérés", CGFloat(340), #selector(switchInput)), ("Mentés", CGFloat(495), #selector(saveSettings))] {
            let b = NSButton(title: text, target: self, action: action); b.frame = NSRect(x: x, y: 20, width: 125, height: 32); root.addSubview(b)
        }
        w.center(); w.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true); refresh()
    }
    @objc func usePreset() {
        let i = preset.indexOfSelectedItem
        if i < 3 { value.stringValue = String((profile.indexOfSelectedItem == 0 ? [144,210,208] : [17,27,15])[i]) }
    }
    @objc func refresh() {
        monitor.removeAllItems(); monitor.addItem(withTitle: "Válassz monitort…")
        DispatchQueue.global().async {
            let result = Result { try Backend.run(["display", "list"]) }
            DispatchQueue.main.async {
                switch result {
                case .success(let output):
                    self.displays = Display.parse(output)
                    self.monitor.removeAllItems(); self.monitor.addItem(withTitle: "Válassz monitort…")
                    for d in self.displays { self.monitor.addItem(withTitle: "\(d.name) [\(d.uuid)]") }
                    if let index = self.displays.firstIndex(where: { $0.uuid == self.config.monitor }) { self.monitor.selectItem(at: index+1) }
                    self.hint.stringValue = self.displays.isEmpty ? "Nincs elérhető külső monitor." : "Monitorlista frissítve (csak olvasás). A váltógomb a legutóbb mentett célra küld."
                case .failure(let error): self.hint.stringValue = error.localizedDescription
                }
            }
        }
    }
    @objc func saveSettings() {
        var candidate = config
        let index = monitor.indexOfSelectedItem - 1
        guard displays.indices.contains(index), let number = Int(value.stringValue) else { hint.stringValue = "Válassz monitort és adj meg egész bemenetkódot."; return }
        candidate.monitor = displays[index].uuid; candidate.value = number
        candidate.profile = profile.indexOfSelectedItem == 0 ? "lg" : "standard"; candidate.hotkey = shortcut.stringValue
        do {
            try candidate.validate()
            let changed = candidate.hotkey != config.hotkey || hotkey == nil
            if changed { try register(candidate.hotkey) }
            do { try Storage.save(candidate) }
            catch { if changed { try? register(config.hotkey) }; throw error }
            config = candidate; setStatus("Beállítások mentve; aktív bemenet ismeretlen.")
        } catch { hint.stringValue = error.localizedDescription }
    }
    @objc func toggleLogin() {
        do {
            if login.state == .on { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() }
            hint.stringValue = "Bejelentkezési állapot: \(SMAppService.mainApp.status.rawValue) (1: aktív, 2: rendszerjóváhagyásra vár)."
        } catch { hint.stringValue = "Automatikus indítás hiba: \(error.localizedDescription)" }
        login.state = SMAppService.mainApp.status == .enabled ? .on : .off
    }
    @objc func showDiagnostics() {
        let w = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 780, height: 520), styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        w.title = "Diagnosztika – csak olvasás"; w.isReleasedWhenClosed = false; diagnostic = w
        let scroll = NSScrollView(frame: w.contentView!.bounds); scroll.autoresizingMask = [.width, .height]; scroll.hasVerticalScroller = true
        let text = NSTextView(frame: scroll.bounds); text.isEditable = false; text.isSelectable = true; text.font = .monospacedSystemFont(ofSize: 12, weight: .regular); text.autoresizingMask = [.width]
        scroll.documentView = text; w.contentView!.addSubview(scroll)
        text.string = "Felderítés…"; w.center(); w.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
        let current = status
        DispatchQueue.global().async {
            let listing = (try? Backend.run(["display", "list", "detailed"])) ?? "A helper/felderítés nem elérhető."
            let log = (try? String(contentsOf: Storage.log, encoding: .utf8)) ?? ""
            DispatchQueue.main.async {
                text.string = "LG InputSwitch 0.1.0\n\(ProcessInfo.processInfo.operatingSystemVersionString)\nApple Silicon / privát IOAVService\nÁllapot: \(current)\nHotkey regisztrálva: \(self.hotkey != nil)\nLogin állapot: \(SMAppService.mainApp.status.rawValue)\nKonfiguráció: \(Storage.config.path)\nNapló: \(Storage.log.path)\n\n\(listing)\n\n\(log)"
            }
        }
    }
    @objc func quit() { NSApp.terminate(nil) }
    func applicationWillTerminate(_ notification: Notification) {
        if let hotkey { UnregisterEventHotKey(hotkey) }; if let handler { RemoveEventHandler(handler) }
    }
}

// Read-only CLI path for packaging verification; never starts the app or writes settings.
if CommandLine.arguments.contains("--diagnose") {
    do { print(try Backend.run(["display", "list", "detailed"])) }
    catch { fputs("\(error.localizedDescription)\n", stderr); exit(1) }
} else {
    let app = NSApplication.shared
    let delegate = App(); app.delegate = delegate; app.run()
}
