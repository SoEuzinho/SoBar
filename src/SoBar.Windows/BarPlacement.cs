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

    // The Shell owns the full strip. Only the content-sized HWND is visible inside it.
    public static PixelRect FitDock(PixelRect strip, BarEdge edge, double lengthDip, uint dpi, int inset)
    {
        if (lengthDip <= 0) return strip;
        var horizontal = edge is BarEdge.Top or BarEdge.Bottom;
        var span = horizontal ? strip.Width : strip.Height;
        var depth = horizontal ? strip.Height : strip.Width;
        int sideMargin = Math.Min(inset, Math.Max(0, (span - 1) / 2));
        int normalMargin = Math.Min(inset, Math.Max(0, (depth - 1) / 2));
        var length = (int)Math.Clamp(Math.Ceiling(lengthDip * dpi / 96d), 1, Math.Max(1, span - 2 * sideMargin));
        var offset = (span - length) / 2;
        return horizontal
            ? new(strip.Left + offset, strip.Top + normalMargin, strip.Left + offset + length, strip.Bottom - normalMargin)
            : new(strip.Left + normalMargin, strip.Top + offset, strip.Right - normalMargin, strip.Top + offset + length);
    }

    public static PixelRect EdgeStrip(PixelRect bounds, PixelRect dock, BarEdge edge, int thickness)
    {
        var edgeStrip = At(bounds, edge, thickness);
        return edge is BarEdge.Top or BarEdge.Bottom
            ? edgeStrip with { Left = dock.Left, Right = dock.Right }
            : edgeStrip with { Top = dock.Top, Bottom = dock.Bottom };
    }

    public static PixelRect HoverArea(PixelRect dock, PixelRect strip) =>
        new(Math.Min(dock.Left, strip.Left), Math.Min(dock.Top, strip.Top),
            Math.Max(dock.Right, strip.Right), Math.Max(dock.Bottom, strip.Bottom));
}

