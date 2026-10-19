using System.Runtime.InteropServices;

namespace LGInputSwitch;

// Definitions follow AMD's MIT-licensed headers and official DDCBlockAccess sample.
// DDCBlockAccess takes destination + source + DDC body (8 bytes total).
internal static class Amd
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)] internal struct Adapter {
        public int Size, Index;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Udid;
        public int Bus, Device, Function, Vendor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string DisplayName;
        public int Present, Exist;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string DriverPath;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string DriverPathExt;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Pnp;
        public int OSDisplayIndex;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)] internal struct DisplayInfo {
        public int LogicalIndex, PhysicalIndex, LogicalAdapter, PhysicalAdapter, Controller;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Manufacturer;
        public int Type, OutputType, Connector, Mask, Flags;
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr Allocate(int size);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Create(Allocate allocator, int connected, out IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Destroy(IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int CountAdapters(IntPtr context, out int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GetAdapters(IntPtr context, IntPtr adapters, int bytes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GetDisplays(IntPtr context, int adapter, out int count, out IntPtr displays, int forceDetect);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DdcBlock(IntPtr context, int adapter, int display, int option, int commandIndex, int sendLength, byte[] send, ref int receiveLength, IntPtr receive);
    static void Check(int rc, string action) { if (rc != 0) throw new Exception($"{action}: AMD ADL {rc}. Ellenőrizd a FirePro/Boot Camp drivert és a HDMI/DDC kapcsolatot; a fizikai váltás nem igazolt."); }
    internal static string Describe() => Use(null, null);
    internal static void Write(string displayName, byte[] bytes) => Use(displayName, bytes);
    static string Use(string? target, byte[]? bytes)
    {
        if (!NativeLibrary.TryLoad(Path.Combine(Environment.SystemDirectory, "atiadlxx.dll"), out IntPtr lib)) throw new Exception("AMD ADL (atiadlxx.dll) nem található. A Mac Pro 6,1 Windowsos FirePro/Boot Camp driverét ellenőrizd.");
        IntPtr context = IntPtr.Zero;
        var allocations = new List<IntPtr>();
        Allocate alloc = size => { var p = Marshal.AllocHGlobal(size); allocations.Add(p); return p; };
        Destroy? destroy = null;
        try {
            T Get<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(lib, name));
            destroy = Get<Destroy>("ADL2_Main_Control_Destroy");
            Check(Get<Create>("ADL2_Main_Control_Create")(alloc, 1, out context), "ADL2_Main_Control_Create");
            Check(Get<CountAdapters>("ADL2_Adapter_NumberOfAdapters_Get")(context, out int count), "AMD adapter darabszám");
            if (count is < 1 or > 256) throw new Exception("Érvénytelen AMD adapter darabszám.");
            int size = Marshal.SizeOf<Adapter>(); IntPtr buffer = alloc(checked(count * size));
            for (int i = 0; i < count; i++) Marshal.StructureToPtr(new Adapter { Size = size }, buffer + i*size, false);
            Check(Get<GetAdapters>("ADL2_Adapter_AdapterInfo_Get")(context, buffer, count*size), "AMD adapter adatok");
            var candidates = new List<(Adapter a, DisplayInfo d)>(); var lines = new List<string>();
            var displays = Get<GetDisplays>("ADL2_Display_DisplayInfo_Get");
            for (int i = 0; i < count; i++) {
                var a = Marshal.PtrToStructure<Adapter>(buffer + i*size);
                lines.Add($"ADL adapter {a.Index}: {a.Name}; {a.DisplayName}; present={a.Present}");
                if (a.Present != 1 || (target != null && !string.Equals(a.DisplayName, target, StringComparison.OrdinalIgnoreCase))) continue;
                Check(displays(context, a.Index, out int n, out IntPtr ds, 0), "AMD display adatok");
                if (n is < 0 or > 256 || (n > 0 && ds == IntPtr.Zero)) throw new Exception("Érvénytelen AMD display lista.");
                var active = new List<DisplayInfo>();
                for (int j = 0; j < n; j++) {
                    var d = Marshal.PtrToStructure<DisplayInfo>(ds + j*Marshal.SizeOf<DisplayInfo>());
                    lines.Add($"  display {d.LogicalIndex}: {d.Name}; flags=0x{d.Flags:X}; logicalAdapter={d.LogicalAdapter}");
                    if ((d.Flags & d.Mask & 3) == 3) active.Add(d);
                }
                // Without QueryDisplayConfig cross-mapping we deliberately support one active
                // display per ADL logical adapter. Never guess among identical monitor names.
                if (target != null) {
                    if (active.Count != 1) throw new Exception("Az AMD logikai adapteren nem pontosan egy aktív monitor van. Az első verzió ebben a topológiában nem tud biztonságosan címezni.");
                    candidates.Add((a, active[0]));
                }
            }
            if (target != null) {
                if (candidates.Count != 1 || bytes == null) throw new Exception("Nem egyértelmű az OS-kijelző és az AMD adapter hozzárendelése.");
                var c = candidates[0]; int receive = 0;
                Check(Get<DdcBlock>("ADL2_Display_DDCBlockAccess_Get")(context, c.a.Index, c.d.LogicalIndex, 0, 0, bytes.Length, bytes, ref receive, IntPtr.Zero), "ADL2_Display_DDCBlockAccess_Get (LG write)");
            }
            return string.Join(Environment.NewLine, lines);
        } finally {
            if (context != IntPtr.Zero) destroy?.Invoke(context);
            foreach (var p in allocations) Marshal.FreeHGlobal(p);
            GC.KeepAlive(alloc); NativeLibrary.Free(lib);
        }
    }
}
