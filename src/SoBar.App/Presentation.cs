using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using SoBar.Core;

namespace SoBar.App;

internal abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new(property));
        return true;
    }
}

internal static class Presentation
{
    public static Button Button(string caption, Action action, string? hint = null)
    {
        var button = new Button { Content = caption, ToolTip = hint };
        button.Click += (_, _) => action();
        return button;
    }
    public static void Apply(AppSettings settings, ResourceDictionary? target = null)
    {
        var light = settings.Theme is "Claro" or "iOS Glass";
        var midnight = settings.Theme == "Midnight";
        var resources = target ?? Application.Current.Resources;
        void ColorBrush(string id, string value)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            resources[id] = brush;
        }
        ColorBrush("Base", light ? "#F4F5F7" : midnight ? "#0E1222" : "#141517");
        ColorBrush("Surface", light ? "#E4E8ED" : midnight ? "#1A2037" : "#232529");
        ColorBrush("Raised", light ? "#D6DFE9" : midnight ? "#2A3556" : "#353941");
        ColorBrush("Line", light ? "#C3CCD7" : midnight ? "#374263" : "#3C424B");
        ColorBrush("Ink", light ? "#16202C" : "#F5F7FA");
        ColorBrush("Muted", light ? "#49596A" : midnight ? "#AFB9DE" : "#B9BBC1");
        ColorBrush("Accent", settings.Accent);
        var glass = settings.Theme is "Liquid Glass" or "iOS Glass";
        Color ColorValue(string value) => (Color)ColorConverter.ConvertFromString(value);
        void Gradient(string key, string top, string bottom, double opacity = 1)
        {
            var brush = new LinearGradientBrush(ColorValue(top),ColorValue(bottom),90) { Opacity = opacity };
            brush.Freeze(); resources[key] = brush;
        }
        Gradient("DockTile", light ? "#CBFFFFFF" : midnight ? "#CE2E354F" : "#D5292A2D",
                             light ? "#BAC9D1DC" : midnight ? "#B2182032" : "#CA151618",glass ? .55 : .93);
        Gradient("DockRim", light ? "#EEFFFFFF" : "#BD939499",light ? "#998997A8" : "#68474950");
        ColorBrush("DockEdge", light ? "#80798497" : "#634D4F54");
        ColorBrush("DockHover", light ? "#E0D8E7F4" : "#CC424953");
        Gradient("BarFill", light ? "#F5F8FB" : midnight ? "#1E263E" : glass ? "#292B2F" : "#222327",
                            light ? "#CFD7E3" : midnight ? "#101623" : "#101114",settings.Opacity * (glass ? .73 : 1));
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);
    [StructLayout(LayoutKind.Sequential)] private struct AccentPolicy { public int State, Flags, Tint, Animation; }
    [StructLayout(LayoutKind.Sequential)] private struct CompositionData { public int Attribute; public nint Data; public int Size; }
    [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(nint hwnd, ref CompositionData data);
    public static void Glass(Window window, AppSettings settings)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0) return;
        var glass = settings.Theme is "Liquid Glass" or "iOS Glass";
        var corner = 2;
        DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
        var dark = settings.Theme is "Claro" or "iOS Glass" ? 0 : 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
        // Accent blur on Windows 11 can replace the transparent pixels of a layered
        // WPF surface with solid black. Preserve native per-pixel alpha on this backend.
        if (OperatingSystem.IsWindowsVersionAtLeast(10,0,22000)) return;
        // Windows 10 has an optional blur-behind compositor for layered surfaces.
        {
            var policy = new AccentPolicy { State = glass ? 3 : 0 };
            var data = new CompositionData { Attribute = 19, Size = Marshal.SizeOf<AccentPolicy>() };
            data.Data = Marshal.AllocHGlobal(data.Size);
            try
            {
                Marshal.StructureToPtr(policy, data.Data, false);
                try { SetWindowCompositionAttribute(hwnd, ref data); } catch (EntryPointNotFoundException) { }
            }
            finally { Marshal.FreeHGlobal(data.Data); }
        }
    }
    public static KeyGesture ParseShortcut(string value)
    {
        var gesture = new KeyGestureConverter().ConvertFromInvariantString(value) as KeyGesture;
        return gesture is { Modifiers: not ModifierKeys.None }
            ? gesture : throw new FormatException("Combine uma tecla com Ctrl, Alt, Shift ou Win.");
    }
}

