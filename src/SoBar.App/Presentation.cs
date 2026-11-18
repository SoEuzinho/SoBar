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
        ColorBrush("Base", light ? "#F3F6F3" : midnight ? "#0E1222" : "#101916");
        ColorBrush("Surface", light ? "#E3EBE5" : midnight ? "#1A2037" : "#1C2824");
        ColorBrush("Raised", light ? "#D1DED5" : midnight ? "#2A3556" : "#2A3933");
        ColorBrush("Line", light ? "#BCCBC1" : midnight ? "#374263" : "#34443E");
        ColorBrush("Ink", light ? "#14281E" : "#F2F8F5");
        ColorBrush("Muted", light ? "#415B4E" : midnight ? "#AFB9DE" : "#ACBEB5");
        ColorBrush("Accent", settings.Accent);
        var fill = settings.Theme switch { "Claro" => "F3F6F3", "iOS Glass" => "E7EFFA", "Midnight" => "161B30", "Liquid Glass" => "172D26", _ => "17211D" };
        var opacity = settings.Opacity * (settings.Theme is "Liquid Glass" or "iOS Glass" ? .72 : 1);
        ColorBrush("BarFill", $"#{(byte)Math.Round(opacity * 255):X2}{fill}");
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
        WindowChrome.SetWindowChrome(window, new WindowChrome {
            CaptionHeight = 0, CornerRadius = new CornerRadius(0),
            ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(-1),
            UseAeroCaptionButtons = false
        });
        var corner = 2;
        DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
        var dark = settings.Theme is "Claro" or "iOS Glass" ? 0 : 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            var type = glass ? 3 : 1; // DWMSBT_TRANSIENTWINDOW / NONE
            DwmSetWindowAttribute(hwnd, 38, ref type, 4);
        }
        else
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

