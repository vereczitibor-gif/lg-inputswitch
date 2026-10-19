using System.Diagnostics;
using System.Text.Json;

namespace LGInputSwitch;

public sealed record Config(string Monitor = "", string Profile = "lg-amd", int Value = 210, string Hotkey = "Ctrl+Alt+S")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Monitor)) throw new Exception("Válassz ki egy monitort.");
        if (Profile is not ("lg-amd" or "lg-nvidia" or "standard") || Value is < 0 or > 65535) throw new Exception("Érvénytelen profil vagy bemenetkód (0–65535).");
        _ = Shortcut.Parse(Hotkey);
    }
}
public record MonitorEntry(string Id, string Name, string DisplayName, string? Limitation)
{
    public override string ToString() => $"{Name} [{DisplayName}]" + (Limitation is null ? "" : $" – {Limitation}");
}
public record Shortcut(uint Modifiers, uint Key)
{
    public static Shortcut Parse(string value)
    {
        var parts = value.ToUpperInvariant().Split('+').Select(s => s.Trim()).ToArray();
        if (parts.Length < 2) throw new Exception("Hotkey: pl. Ctrl+Alt+S; A–Z, 0–9, F1–F11.");
        uint mods = 0;
        foreach (string p in parts[..^1])
        {
            uint m = p switch { "CTRL" => 2, "ALT" => 1, "SHIFT" => 4, "WIN" => 8, _ => throw new Exception("Módosítók: Ctrl, Alt, Shift, Win.") };
            if ((mods & m) != 0) throw new Exception("Ismételt módosító.");
            mods |= m;
        }
        if ((mods & 11) == 0) throw new Exception("Legalább Ctrl, Alt vagy Win szükséges.");
        string key = parts[^1]; uint vk;
        if (key.Length == 1 && (key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')) vk = key[0];
        else if (key.StartsWith('F') && int.TryParse(key[1..], out int f) && f is >= 1 and <= 11) vk = (uint)(111 + f);
        else throw new Exception("Támogatott billentyűk: A–Z, 0–9, F1–F11 (F12 foglalt). ");
        return new Shortcut(mods | 0x4000, vk);
    }
}
public static class Packet
{
    public static byte[] AmdLG(ushort value) => [0x6e, 0x50, ..LG(value)];
    public static byte[] LG(ushort value)
    {
        byte[] bytes = [0x84, 0x03, 0xF4, (byte)(value >> 8), (byte)value, 0];
        byte xor = 0x6E ^ 0x50;
        foreach (byte b in bytes[..5]) xor ^= b;
        bytes[5] = xor; return bytes;
    }
}
public sealed class SwitchGate
{
    private double until = double.NegativeInfinity;
    public bool Begin(double now) { if (now < until) return false; until = now + 5; return true; }
}
public static class Store
{
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGInputSwitch");
    public static string ConfigPath => Path.Combine(Folder, "config.json");
    public static string LogPath => Path.Combine(Folder, "diagnostics.log");
    public static void Save(Config c)
    {
        Directory.CreateDirectory(Folder);
        string temp = ConfigPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(c, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, ConfigPath, true);
    }
    public static void Log(string text)
    {
        try {
            Directory.CreateDirectory(Folder);
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 262144) File.Move(LogPath, LogPath + ".old", true);
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {text}\n");
        } catch { /* Status is still visible if the local log cannot be written. */ }
    }
}
