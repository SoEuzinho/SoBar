using System.Runtime.InteropServices;

namespace SoBar.Windows;

public sealed record DesktopMonitor(string Id, string Name, bool IsPrimary);
internal sealed record DisplayInfo(nint Handle, string Id, PixelRect Bounds, PixelRect Work, bool Primary, uint Dpi)
{
    public static List<DisplayInfo> ReadAll()
    {
        var displays = new List<DisplayInfo>();
        Native.EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
            if (Native.GetMonitorInfo(monitor, ref info))
            {
                uint dpi = Native.GetDpiForMonitor(monitor, 0, out uint value, out _) == 0 ? value : 96;
                displays.Add(new(monitor, info.Device, info.Bounds, info.Work, (info.Flags & 1) != 0, dpi));
            }
            return true;
        }, 0);
        return displays;
    }
}

internal enum BarEdge : uint { Left, Top, Right, Bottom }

internal static class BarPlacement
{
    public static PixelRect At(PixelRect rect, BarEdge edge, int thickness) => edge switch
    {
        BarEdge.Top => rect with { Bottom = Math.Min(rect.Bottom, rect.Top + thickness) },
        BarEdge.Bottom => rect with { Top = Math.Max(rect.Top, rect.Bottom - thickness) },
        BarEdge.Left => rect with { Right = Math.Min(rect.Right, rect.Left + thickness) },
        _ => rect with { Left = Math.Max(rect.Left, rect.Right - thickness) }
    };

    public static bool Covers(PixelRect frame, PixelRect monitor) =>
        frame.Left <= monitor.Left + 1 && frame.Top <= monitor.Top + 1 &&
        frame.Right >= monitor.Right - 1 && frame.Bottom >= monitor.Bottom - 1;

    public static int PhysicalThickness(int logicalThickness, uint dpi) =>
        Math.Max(1, (int)Math.Round(Math.Clamp(logicalThickness, 24, 240) * dpi / 96d));
}

