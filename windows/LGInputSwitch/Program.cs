using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.ComponentModel;
using Microsoft.Win32;

namespace LGInputSwitch;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        // Helpers run before mutex/UI creation; only --worker-switch can send a write.
        if (args.Length > 0 && args[0].StartsWith("--worker-")) {
            Console.OutputEncoding = new UTF8Encoding(false);
            try {
                string output = args[0] switch {
                    "--worker-list" => JsonSerializer.Serialize(Native.Enumerate()),
                    "--worker-diagnose" => Diagnose(),
                    "--worker-switch" when args.Length == 2 => DoSwitch(args[1]),
                    _ => throw new Exception("Ismeretlen helper parancs.")
                };
                Console.Write(output); Environment.ExitCode = 0;
            } catch (Exception e) { Console.Error.Write(e.Message); Environment.ExitCode = 1; }
            return;
        }
        using var mutex = new Mutex(true, "Local\\LGInputSwitch.Tray", out bool created);
        if (!created) { MessageBox.Show("Az LG InputSwitch már fut; keresd a tálcaikonját."); return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
    static string DoSwitch(string encoded) {
        var config = JsonSerializer.Deserialize<Config>(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))) ?? throw new Exception("Hiányzó konfiguráció.");
        Native.Switch(config);
        return $"Parancs elküldve ({config.Profile}, {config.Value}); a fizikai váltás NINCS igazolva.";
    }
    static string Diagnose() {
        string amd; try { amd = Amd.Describe(); } catch (Exception e) { amd = e.Message; }
        return $"LG InputSwitch 0.1.0\n{Environment.OSVersion}\nx64={Environment.Is64BitProcess}\n{JsonSerializer.Serialize(Native.Enumerate(), new JsonSerializerOptions { WriteIndented = true })}\n\n{amd}";
    }
}

internal static class Worker
{
    internal static async Task<string> Run(string action, Config? config = null)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        info.ArgumentList.Add(action);
        if (config != null) info.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config))));
        using var process = Process.Start(info) ?? throw new Exception("A DDC helper nem indítható.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new Exception("DDC időtúllépés. A váltás eredménye ismeretlen; nincs automatikus újraküldés.");
        }
        string output = await stdout, error = await stderr;
        if (process.ExitCode != 0) throw new Exception($"DDC/helper hiba ({process.ExitCode}): {error}");
        return output;
    }
}

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    internal Action? Pressed, Wake;
    int id = 1;
    internal bool Registered { get; private set; }
    internal HotkeyWindow() { CreateHandle(new CreateParams { Caption = "LGInputSwitch.Hotkey" }); }
    internal void Register(string shortcut) {
        var s = Shortcut.Parse(shortcut); int next = id == 1 ? 2 : 1;
        Native.Check(Native.RegisterHotKey(Handle, next, s.Modifiers, s.Key), "A hotkey foglalt vagy a rendszer fenntartja");
        if (Registered) Native.UnregisterHotKey(Handle, id);
        id = next; Registered = true;
    }
    protected override void WndProc(ref Message m) {
        if (m.Msg == 0x0312 && m.WParam.ToInt32() == id) Pressed?.Invoke();
        if (m.Msg == 0x0218 && m.WParam.ToInt32() is 7 or 18) Wake?.Invoke();
        base.WndProc(ref m);
    }
    public void Dispose() { if (Registered) Native.UnregisterHotKey(Handle, id); DestroyHandle(); }
}

internal sealed class TrayApp : ApplicationContext
{
    Config config = new();
    readonly NotifyIcon tray;
    readonly HotkeyWindow hotkey = new();
    readonly SwitchGate gate = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly ToolStripMenuItem switchItem = new(), stateItem = new() { Enabled = false };
    bool busy;
    string status = "Váltás még nem történt; aktív bemenet ismeretlen.";
    Form? settings;
    internal TrayApp() {
        try { if (File.Exists(Store.ConfigPath)) { config = JsonSerializer.Deserialize<Config>(File.ReadAllText(Store.ConfigPath)) ?? throw new Exception("Üres konfiguráció."); config.Validate(); } }
        catch (Exception e) { config = new(); status = $"Konfigurációs hiba: {e.Message}"; }
        var menu = new ContextMenuStrip();
        switchItem.Click += async (_, _) => await Switch(); menu.Items.Add(switchItem); menu.Items.Add(stateItem); menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beállítások…", null, (_, _) => ShowSettings());
        menu.Items.Add("Diagnosztika (csak olvasás)…", null, async (_, _) => await Diagnostics());
        menu.Items.Add("Kilépés", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "LG InputSwitch", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => ShowSettings();
        hotkey.Pressed = async () => await Switch();
        hotkey.Wake = () => Status("Felébredés: a következő kérés frissen felderíti a kiválasztott monitort.");
        try { hotkey.Register(config.Hotkey); } catch (Exception e) { status = e.Message; }
        Status(status);
        if (config.Monitor.Length == 0) ShowSettings();
    }
    void Status(string text) {
        status = text; Store.Log(text);
        switchItem.Text = $"Váltási kérés → {config.Value} [{config.Hotkey}]";
        switchItem.Enabled = !busy && config.Monitor.Length != 0;
        stateItem.Text = text.Length > 100 ? text[..100] + "…" : text;
    }
    async Task Switch() {
        try { config.Validate(); } catch (Exception e) { Status(e.Message); ShowSettings(); return; }
        if (busy || !gate.Begin(clock.Elapsed.TotalSeconds)) return;
        busy = true; Status("Váltási kérés küldése…");
        try { Status(await Worker.Run("--worker-switch", config)); }
        catch (Exception e) {
            Status(e.Message);
            tray.ShowBalloonTip(5000, "LG InputSwitch – hiba", e.Message, ToolTipIcon.Warning);
        } finally { busy = false; switchItem.Enabled = config.Monitor.Length != 0; }
    }
    static string RunValue => $"\"{Environment.ProcessPath}\"";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static bool LoginEnabled() { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return Equals(key?.GetValue("LGInputSwitch"), RunValue); }
    static void SetLogin(bool enabled) {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue("LGInputSwitch", RunValue); else key.DeleteValue("LGInputSwitch", false);
    }
    void ShowSettings() {
        if (settings != null && !settings.IsDisposed) { settings.Show(); settings.Activate(); return; }
        var w = new Form { Text = "LG InputSwitch", ClientSize = new Size(625, 435), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, StartPosition = FormStartPosition.CenterScreen, AutoScaleMode = AutoScaleMode.Dpi };
        settings = w;
        var monitors = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        var profile = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        profile.Items.AddRange(["LG / AMD FirePro (Mac Pro 6,1)", "LG / NVIDIA", "Szabványos VCP 0x60"]);
        profile.SelectedIndex = config.Profile switch { "lg-amd" => 0, "lg-nvidia" => 1, _ => 2 };
        var preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        preset.Items.AddRange(["Thunderbolt / USB-C", "HDMI 1", "DisplayPort 1", "Egyedi kód"]); preset.SelectedIndex = 3;
        var code = new NumericUpDown { Minimum = 0, Maximum = 65535, Value = config.Value };
        var shortcut = new TextBox { Text = config.Hotkey };
        var login = new CheckBox { Text = "Indítás a Windowszal (külön, azonnal alkalmazódik)", AutoSize = true };
        var hint = new Label { Text = "A mentés nem vált bemenetet. A hotkeyhez Ctrl/Alt/Shift/Win használható.", AutoSize = false };
        void Row(string label, Control control, int y) {
            w.Controls.Add(new Label { Text = label, Bounds = new Rectangle(15, y+5, 145, 25) });
            control.Bounds = new Rectangle(165, y, 440, 28); w.Controls.Add(control);
        }
        Row("Monitor", monitors, 20); Row("Protokoll", profile, 70); Row("Célbemenet", preset, 120); Row("Bemenetkód (dec.)", code, 170); Row("Globális shortcut", shortcut, 220);
        login.Location = new Point(165, 270); w.Controls.Add(login);
        try { login.Checked = LoginEnabled(); } catch (Exception e) { hint.Text = e.Message; }
        login.Click += (_, _) => {
            try { SetLogin(login.Checked); hint.Text = login.Checked ? "Automatikus indítás bekapcsolva." : "Automatikus indítás kikapcsolva."; }
            catch (Exception e) { hint.Text = e.Message; }
            try { login.Checked = LoginEnabled(); } catch { }
        };
        hint.Bounds = new Rectangle(15, 310, 590, 65); w.Controls.Add(hint);
        var refresh = new Button { Text = "Lista frissítése", Bounds = new Rectangle(15, 385, 130, 32) };
        var manual = new Button { Text = "Váltási kérés", Bounds = new Rectangle(300, 385, 140, 32) };
        var save = new Button { Text = "Mentés", Bounds = new Rectangle(475, 385, 130, 32) };
        w.Controls.AddRange([refresh, manual, save]);
        void Preset() { if (preset.SelectedIndex < 3) code.Value = (profile.SelectedIndex == 2 ? new[] {27,17,15} : new[] {210,144,208})[preset.SelectedIndex]; }
        profile.SelectedIndexChanged += (_, _) => Preset(); preset.SelectedIndexChanged += (_, _) => Preset();
        async Task Refresh() {
            refresh.Enabled = save.Enabled = false; hint.Text = "Monitorok felderítése (csak olvasás)…";
            try {
                var list = JsonSerializer.Deserialize<List<MonitorEntry>>(await Worker.Run("--worker-list")) ?? [];
                if (w.IsDisposed) return;
                monitors.Items.Clear(); monitors.Items.Add("Válassz monitort…"); monitors.Items.AddRange(list.Cast<object>().ToArray()); monitors.SelectedIndex = 0;
                for (int i = 0; i < list.Count; i++) if (list[i].Id == config.Monitor) monitors.SelectedIndex = i+1;
                hint.Text = list.Count == 0 ? "Nincs elérhető monitor." : "Lista frissítve. A mentés nem küld DDC-írást.";
            } catch (Exception e) { if (!w.IsDisposed) hint.Text = e.Message; }
            finally { if (!w.IsDisposed) refresh.Enabled = save.Enabled = true; }
        }
        refresh.Click += async (_, _) => await Refresh(); w.Shown += async (_, _) => await Refresh();
        save.Click += (_, _) => {
            try {
                if (monitors.SelectedItem is not MonitorEntry monitor) throw new Exception("Válassz monitort.");
                var candidate = new Config(monitor.Id, profile.SelectedIndex switch { 0 => "lg-amd", 1 => "lg-nvidia", _ => "standard" }, (int)code.Value, shortcut.Text);
                candidate.Validate(); bool changed = candidate.Hotkey != config.Hotkey || !hotkey.Registered;
                if (changed) hotkey.Register(candidate.Hotkey);
                try { Store.Save(candidate); } catch { if (changed) hotkey.Register(config.Hotkey); throw; }
                config = candidate; Status("Beállítások mentve; az aktív bemenet ismeretlen."); hint.Text = status;
            } catch (Exception e) { hint.Text = e.Message; }
        };
        manual.Click += async (_, _) => { await Switch(); if (!w.IsDisposed) hint.Text = status; };
        w.Show();
    }
    async Task Diagnostics() {
        var w = new Form { Text = "Diagnosztika – csak olvasás", Size = new Size(800,600), StartPosition = FormStartPosition.CenterScreen };
        var text = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 10), Text = "Felderítés…" };
        var export = new Button { Dock = DockStyle.Bottom, Text = "Mentés fájlba…", Height = 35 };
        export.Click += (_, _) => { using var dialog = new SaveFileDialog { FileName = "LGInputSwitch-diagnostics.txt", Filter = "Szöveg|*.txt" }; if (dialog.ShowDialog(w) == DialogResult.OK) { try { File.WriteAllText(dialog.FileName, text.Text); } catch (Exception e) { MessageBox.Show(e.Message); } } };
        w.Controls.Add(text); w.Controls.Add(export); w.Show();
        try {
            string result = await Worker.Run("--worker-diagnose");
            string log = File.Exists(Store.LogPath) ? await File.ReadAllTextAsync(Store.LogPath) : "";
            if (!w.IsDisposed) text.Text = $"{result}\r\nHotkey regisztrálva: {hotkey.Registered}\r\nÁllapot: {status}\r\nKonfiguráció: {Store.ConfigPath}\r\nNapló: {Store.LogPath}\r\n\r\n{log}".Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
        } catch (Exception e) { if (!w.IsDisposed) text.Text = e.Message; }
    }
    protected override void ExitThreadCore() { tray.Visible = false; tray.Dispose(); hotkey.Dispose(); base.ExitThreadCore(); }
}
