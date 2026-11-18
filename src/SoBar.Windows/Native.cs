using System.Runtime.InteropServices;
using System.Text;

namespace SoBar.Windows;

[StructLayout(LayoutKind.Sequential)]
internal record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;
    public readonly bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo
    {
        public int Size;
        public PixelRect Bounds, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AppBarData
    {
        public int Size;
        public nint Handle;
        public uint Message, Edge;
        public PixelRect Rect;
        public nint Param;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ShellFileInfo
    {
        public nint Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ThumbnailProperties
    {
        public uint Flags;
        public PixelRect Destination, Source;
        public byte Opacity;
        public int Visible, ClientOnly;
    }

    internal delegate bool EnumWindowsCallback(nint handle, nint state);
    internal delegate bool EnumMonitorsCallback(nint monitor, nint dc, nint rect, nint state);

    [DllImport("user32")] internal static extern bool EnumWindows(EnumWindowsCallback callback, nint state);
    [DllImport("user32")] internal static extern bool EnumDisplayMonitors(nint dc, nint clip, EnumMonitorsCallback callback, nint state);
    [DllImport("user32", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("shcore")] internal static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
    [DllImport("user32")] internal static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32")] internal static extern bool IsWindow(nint hwnd);
    [DllImport("user32")] internal static extern bool IsIconic(nint hwnd);
    [DllImport("user32")] internal static extern bool IsZoomed(nint hwnd);
    [DllImport("user32")] internal static extern bool ShowWindowAsync(nint hwnd, int command);
    [DllImport("user32")] internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32")] internal static extern nint GetForegroundWindow();
    [DllImport("user32")] internal static extern nint GetShellWindow();
    [DllImport("user32")] internal static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32", EntryPoint = "GetWindowLongW")] internal static extern int GetWindowLong(nint hwnd, int index);
    [DllImport("user32", EntryPoint = "GetClassLongPtrW")] private static extern nint GetClassLong64(nint hwnd, int index);
    [DllImport("user32", EntryPoint = "GetClassLongW")] private static extern uint GetClassLong32(nint hwnd, int index);
    internal static nint GetClassIcon(nint hwnd, int index) => IntPtr.Size == 8 ? GetClassLong64(hwnd, index) : (nint)GetClassLong32(hwnd, index);
    [DllImport("user32")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(nint hwnd, StringBuilder text, int size);
    [DllImport("user32", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint hwnd, StringBuilder text, int size);
    [DllImport("kernel32", SetLastError = true)] internal static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] internal static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder buffer, ref int length);
    [DllImport("kernel32")] internal static extern bool CloseHandle(nint handle);
    [DllImport("user32", CharSet = CharSet.Unicode)] internal static extern nint SendMessageTimeout(nint hwnd, uint message, nint wParam, nint lParam, uint flags, uint timeout, out nint result);
    [DllImport("shell32", CharSet = CharSet.Unicode)] internal static extern nint SHGetFileInfo(string path, uint attributes, ref ShellFileInfo info, uint size, uint flags);
    [DllImport("user32")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("dwmapi", EntryPoint = "DwmGetWindowAttribute")] internal static extern int GetCloaked(nint hwnd, int attribute, out int value, int size);
    [DllImport("dwmapi", EntryPoint = "DwmGetWindowAttribute")] internal static extern int GetFrame(nint hwnd, int attribute, out PixelRect bounds, int size);
    [DllImport("user32")] internal static extern bool GetWindowRect(nint hwnd, out PixelRect rect);
    [DllImport("user32")] internal static extern bool GetClientRect(nint hwnd, out PixelRect rect);
    [DllImport("user32")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
    [DllImport("shell32")] internal static extern nuint SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32")] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32")] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("dwmapi")] internal static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi")] internal static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi")] internal static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out Point size);
    [DllImport("dwmapi")] internal static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref ThumbnailProperties properties);
}

