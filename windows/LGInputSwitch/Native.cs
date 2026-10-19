using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LGInputSwitch;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct Physical {
        public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct MonitorInfo {
        public int Size; public Rect Monitor, Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct DisplayDevice {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    internal delegate bool MonitorProc(IntPtr monitor, IntPtr dc, ref Rect r, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorProc callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice display, uint flags);
    [DllImport("dxva2.dll", SetLastError = true)] internal static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);
    [DllImport("dxva2.dll", SetLastError = true)] internal static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] Physical[] physical);
    [DllImport("dxva2.dll", SetLastError = true)] internal static extern bool DestroyPhysicalMonitors(uint count, Physical[] physical);
    [DllImport("dxva2.dll", SetLastError = true)] internal static extern bool SetVCPFeature(IntPtr physical, byte code, uint value);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    internal static void Check(bool result, string action) { if (!result) throw new Win32Exception(Marshal.GetLastWin32Error(), action); }

    // Handles live only within one fresh enumeration, never persisted across sleep or switching.
    internal static List<MonitorEntry> Enumerate(Action<MonitorEntry, IntPtr>? selected = null)
    {
        var list = new List<MonitorEntry>(); Exception? failure = null;
        MonitorProc callback = (IntPtr h, IntPtr dc, ref Rect r, IntPtr data) => {
            try {
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                Check(GetMonitorInfo(h, ref info), "GetMonitorInfo");
                var dev = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
                if (!EnumDisplayDevices(info.Device, 0, ref dev, 1) || string.IsNullOrEmpty(dev.Id)) return true;
                var second = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
                string? limitation = EnumDisplayDevices(info.Device, 1, ref second, 1) ? "Több monitor/klón: nem biztonságosan címezhető" : null;
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(h, out uint count)) limitation ??= $"DDC handle nem elérhető ({Marshal.GetLastWin32Error()})";
                else if (count == 0) limitation ??= "DDC handle nem elérhető (0 fizikai monitor)";
                else if (count != 1) limitation ??= $"{count} fizikai monitor: nem támogatott topológia";
                var entry = new MonitorEntry(dev.Id, dev.Description, info.Device, limitation);
                list.Add(entry); selected?.Invoke(entry, h); return true;
            } catch (Exception e) { failure = e; return false; }
        };
        bool ok = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        if (failure != null) throw failure;
        Check(ok, "EnumDisplayMonitors");
        return list;
    }
    internal static void Switch(Config c)
    {
        c.Validate(); bool found = false;
        // Collect first, reject ambiguous identity before sending any command.
        var list = Enumerate();
        if (list.Count(d => d.Id == c.Monitor) != 1) throw new Exception("A mentett monitor nem érhető el egyértelműen; nincs automatikus helyettesítés.");
        Enumerate((entry, h) => {
            if (entry.Id != c.Monitor) return;
            if (entry.Limitation != null && (c.Profile == "standard" || !entry.Limitation.StartsWith("DDC handle"))) throw new Exception(entry.Limitation);
            found = true;
            if (c.Profile == "lg-amd") { Amd.Write(entry.DisplayName, Packet.AmdLG((ushort)c.Value)); return; }
            if (c.Profile == "lg-nvidia") { Nvidia.Write(entry.DisplayName, Packet.LG((ushort)c.Value)); return; }
            Check(GetNumberOfPhysicalMonitorsFromHMONITOR(h, out uint count), "Fizikai monitor darabszám");
            if (count != 1) throw new Exception("A topológia megváltozott; ismételd meg a felderítést.");
            var monitors = new Physical[count];
            Check(GetPhysicalMonitorsFromHMONITOR(h, count, monitors), "Fizikai monitor handle");
            try { Check(SetVCPFeature(monitors[0].Handle, 0x60, (uint)c.Value), "SetVCPFeature 0x60"); }
            finally { DestroyPhysicalMonitors(count, monitors); }
        });
        if (!found) throw new Exception("A monitor a felderítés közben eltűnt.");
    }
}

// ABI and interface IDs from NVIDIA's official MIT-licensed NVAPI headers.
internal static class Nvidia
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)] internal struct I2CInfo {
        public uint Version, DisplayMask;
        public byte IsDDC, Address;
        public IntPtr Register;
        public uint RegisterSize;
        public IntPtr Data;
        public uint Size, Speed, SpeedKhz;
        public byte PortId;
        public uint IsPortIdSet;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Query(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Init();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] delegate int GetDisplay([MarshalAs(UnmanagedType.LPStr)] string name, out IntPtr display);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GetGPUs(IntPtr display, [Out] IntPtr[] gpus, out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int GetOutput(IntPtr display, out uint mask);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int WriteI2C(IntPtr gpu, ref I2CInfo info);
    static void Check(int code, string action) { if (code != 0) throw new Exception($"{action}: NVAPI {code}. NVIDIA által közvetlenül meghajtott kijelző és megfelelő driver szükséges; Intel/AMD LG-nyers írás ebben a verzióban nem támogatott."); }
    internal static unsafe void Write(string displayName, byte[] packet)
    {
        string dll = Path.Combine(Environment.SystemDirectory, "nvapi64.dll");
        if (!NativeLibrary.TryLoad(dll, out IntPtr library)) throw new Exception("NVAPI nem található. Az LG 0x50-címzéshez ez a verzió NVIDIA GPU-t igényel. Intel/AMD esetén nincs megvalósított nyers DDC backend.");
        bool initialized = false;
        Init? unload = null;
        try {
            var query = Marshal.GetDelegateForFunctionPointer<Query>(NativeLibrary.GetExport(library, "nvapi_QueryInterface"));
            T Get<T>(uint id) where T : Delegate {
                IntPtr p = query(id); if (p == IntPtr.Zero) throw new Exception($"Hiányzó NVAPI interfész: 0x{id:X8}");
                return Marshal.GetDelegateForFunctionPointer<T>(p);
            }
            unload = Get<Init>(0xd22bdd7e);
            Check(Get<Init>(0x0150e828)(), "NvAPI_Initialize"); initialized = true;
            Check(Get<GetDisplay>(0x35c29134)(displayName, out IntPtr display), "Kijelző–NVIDIA hozzárendelés");
            var gpus = new IntPtr[64]; Check(Get<GetGPUs>(0x34ef9506)(display, gpus, out uint count), "GPU felderítés");
            if (count != 1) throw new Exception("Több vagy nulla NVIDIA GPU: nincs egyértelmű cél.");
            Check(Get<GetOutput>(0xd995937e)(display, out uint mask), "Kijelző kimenet");
            if (mask == 0 || (mask & (mask-1)) != 0) throw new Exception("Nem egyértelmű NVIDIA kimenet.");
            byte source = 0x50;
            fixed (byte* data = packet) {
                var info = new I2CInfo { Version = (uint)Marshal.SizeOf<I2CInfo>() | (3u << 16), DisplayMask = mask, IsDDC = 1, Address = 0x6e, Register = (IntPtr)(&source), RegisterSize = 1, Data = (IntPtr)data, Size = (uint)packet.Length, Speed = 0xffff, SpeedKhz = 0 };
                Check(Get<WriteI2C>(0xe812eb07)(gpus[0], ref info), "NvAPI_I2CWrite");
            }
        } finally { if (initialized) unload?.Invoke(); NativeLibrary.Free(library); }
    }
}
