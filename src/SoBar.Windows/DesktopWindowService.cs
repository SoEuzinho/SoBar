using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SoBar.Windows;

public sealed record DesktopWindow(nint Handle, string Title, string ExecutablePath, ImageSource? Icon, bool IsActive, string MonitorId);

public sealed class DesktopWindowService
{
    private sealed record CachedWindow(uint Pid, string Path, ImageSource? Icon, long ExpiresAt);
    private readonly Dictionary<nint, CachedWindow> cache = [];
    private readonly object sync = new();

    public IReadOnlyList<DesktopWindow> List()
    {
        lock (sync)
        {
            var live = new HashSet<nint>();
            var result = new List<DesktopWindow>();
            var monitors = DisplayInfo.ReadAll();
            var foreground = Native.GetForegroundWindow();
            var shell = Native.GetShellWindow();
            var now = Environment.TickCount64;
            Native.EnumWindows((hwnd, _) =>
            {
                if (hwnd == shell || !IsApplicationWindow(hwnd, out var pid)) return true;
                var title = TitleOf(hwnd);
                if (title.Length == 0) return true;
                live.Add(hwnd);
                if (!cache.TryGetValue(hwnd, out var entry) || entry.Pid != pid || entry.ExpiresAt <= now)
                {
                    var path = ExecutableOf(pid);
                    var icon = WindowIcon(hwnd) ?? IconForPath(path);
                    // FrameHost represents multiple packaged apps; launching it would not reopen this app.
                    if (Path.GetFileName(path).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase)) path = "";
                    entry = new(pid, path, icon, now + 120_000);
                    cache[hwnd] = entry;
                }
                var display = Native.MonitorFromWindow(hwnd, 2);
                var monitorId = monitors.FirstOrDefault(m => m.Handle == display)?.Id ?? "";
                result.Add(new(hwnd, title, entry.Path, entry.Icon, hwnd == foreground, monitorId));
                return true;
            }, 0);
            foreach (var hwnd in cache.Keys.Where(key => !live.Contains(key)).ToArray()) cache.Remove(hwnd);
            return result;
        }
    }

    public bool Activate(nint hwnd)
    {
        if (!IsApplicationWindow(hwnd, out _)) return false;
        if (Native.IsIconic(hwnd)) Native.ShowWindowAsync(hwnd, 9); // SW_RESTORE
        return Native.SetForegroundWindow(hwnd);
    }

    public bool Toggle(nint hwnd)
    {
        if (Native.GetForegroundWindow() != hwnd || Native.IsIconic(hwnd)) return Activate(hwnd);
        return IsApplicationWindow(hwnd, out _) && Native.ShowWindowAsync(hwnd, 6); // SW_MINIMIZE
    }

    internal static bool IsApplicationWindow(nint hwnd, out uint pid)
    {
        Native.GetWindowThreadProcessId(hwnd, out pid);
        if (pid == 0 || pid == Environment.ProcessId || !Native.IsWindowVisible(hwnd)) return false;
        int style = Native.GetWindowLong(hwnd, -20);
        const int appWindow = 0x40000, toolWindow = 0x80, noActivate = 0x08000000;
        if ((style & appWindow) == 0 && ((style & (toolWindow | noActivate)) != 0 || Native.GetWindow(hwnd, 4) != 0)) return false;
        if (Native.GetCloaked(hwnd, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
        return !IsShellSurface(hwnd);
    }

    internal static bool IsShellSurface(nint hwnd)
    {
        var name = new StringBuilder(128);
        Native.GetClassName(hwnd, name, name.Capacity);
        return name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW";
    }

    internal static string TitleOf(nint hwnd)
    {
        var title = new StringBuilder(1024);
        return Native.GetWindowText(hwnd, title, title.Capacity) > 0 ? title.ToString() : "";
    }

    private static string ExecutableOf(uint pid)
    {
        var process = Native.OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
        if (process == 0) return "";
        try
        {
            var path = new StringBuilder(32768);
            int length = path.Capacity;
            return Native.QueryFullProcessImageName(process, 0, path, ref length) ? path.ToString() : "";
        }
        finally { Native.CloseHandle(process); }
    }

    private static ImageSource? WindowIcon(nint hwnd)
    {
        Native.SendMessageTimeout(hwnd, 0x7f, 2, 0, 0x0002, 30, out var icon); // WM_GETICON / ICON_SMALL2
        if (icon == 0) icon = Native.GetClassIcon(hwnd, -34);
        if (icon == 0) icon = Native.GetClassIcon(hwnd, -14);
        return CopyIcon(icon);
    }

    public static ImageSource? IconForPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var info = new Native.ShellFileInfo();
        if (Native.SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<Native.ShellFileInfo>(), 0x100 | 1) == 0 || info.Icon == 0) return null;
        try { return CopyIcon(info.Icon); }
        finally { Native.DestroyIcon(info.Icon); }
    }

    private static ImageSource? CopyIcon(nint handle)
    {
        if (handle == 0) return null;
        try
        {
            var icon = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            icon.Freeze();
            return icon;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            return null; // The owning application can destroy its HICON while we enumerate windows.
        }
    }
}

