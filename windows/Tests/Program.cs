using LGInputSwitch;
using System.Runtime.InteropServices;
using System.Text.Json;
static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
static void Reject(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection"); }
for (int v=0; v<=65535; v++) {
    byte[] p = Packet.AmdLG((ushort)v);
    Assert(p.Length == 8 && p[0] == 0x6e && p[1] == 0x50 && p[2] == 0x84 && p[3] == 3 && p[4] == 0xf4, "LG frame");
    Assert(p[5] == v >> 8 && p[6] == (v & 255), "Big endian");
    Assert(p.Aggregate((byte)0, (a,b) => (byte)(a^b)) == 0, "Checksum");
}
Assert(Convert.ToHexString(Packet.AmdLG(144)) == "6E508403F40090DD", "HDMI golden vector");
Assert(Convert.ToHexString(Packet.AmdLG(210)) == "6E508403F400D29F", "TB golden vector");
Assert(Packet.LG(0x4d).Length == 6 && Packet.LG(0x4d)[5] == 0, "Zero checksum preserved");
foreach (var s in new[] {"S", "Ctrl++S", "Ctrl+Ctrl+S", "Ctrl+F12", "Shift+S", "Ctrl+🍎"}) Reject(() => Shortcut.Parse(s));
Assert(Shortcut.Parse("Ctrl+Alt+S") == new Shortcut(0x4003, 0x53), "Hotkey ABI");
Reject(() => new Config().Validate());
Reject(() => new Config("monitor", Value:65536).Validate());
var c = new Config("explicit-monitor"); c.Validate(); Assert(JsonSerializer.Deserialize<Config>(JsonSerializer.Serialize(c)) == c, "Config round trip");
var gate = new SwitchGate(); Assert(gate.Begin(0) && !gate.Begin(0) && !gate.Begin(4.999) && gate.Begin(5), "Cooldown");
Assert(Marshal.SizeOf<Nvidia.I2CInfo>() == 64, "NVAPI x64 structure size");
Assert(Marshal.OffsetOf<Nvidia.I2CInfo>("Data").ToInt32() == 32, "NVAPI pointer offset");
Assert(Marshal.SizeOf<Amd.Adapter>() == 1572 && Marshal.SizeOf<Amd.DisplayInfo>() == 552, "ADL ABI sizes");
Assert(Marshal.OffsetOf<Amd.Adapter>("DisplayName").ToInt32() == 536, "ADL display mapping field");
Console.WriteLine("Windows core tests: all 65536 packet values, golden frames, hotkey validation, config, gate and x64 interop layouts passed. No native API or monitor write invoked.");
