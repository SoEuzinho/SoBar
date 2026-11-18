using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace SoBar.Widgets;

internal static class CardCallout
{
    public static void Configure(Window panel, FrameworkElement anchor, FrameworkElement content, bool dismissOnBlur)
    {
        panel.WindowStyle = WindowStyle.None;
        panel.AllowsTransparency = true;
        panel.ResizeMode = ResizeMode.NoResize;
        panel.WindowStartupLocation = WindowStartupLocation.Manual;
        panel.ShowInTaskbar = false;
        panel.Background = Brushes.Transparent;
        panel.Foreground = Brushes.White;
        // Keep the flyout readable when the parent dock uses its light palette.
        foreach (var (key, hex) in new[] { ("Base","#17212F"), ("Ink","#F8FAFC"), ("Muted","#BFCDDE"),
            ("Surface","#253347"), ("Raised","#384D65"), ("Line","#47586C"), ("Accent","#A3CBFF") })
            panel.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        panel.Content = new Border
        {
            Child = content, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(135,139,161,185)),
            Background = new LinearGradientBrush(Color.FromArgb(209,34,46,63), Color.FromArgb(226,14,24,37), 90)
        };
        var target = anchor.PointToScreen(new Point(anchor.ActualWidth / 2, anchor.ActualHeight / 2));
        var monitor = MonitorFromPoint(new NativePoint((int)target.X,(int)target.Y), 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return;
        var work = info.Work;
        var dpi = VisualTreeHelper.GetDpi(anchor);
        panel.MaxHeight = Math.Max(80,(work.Bottom - work.Top) / dpi.DpiScaleY - 14);
        panel.MaxWidth = Math.Max(80,(work.Right - work.Left) / dpi.DpiScaleX - 14);
        panel.Left = target.X / dpi.DpiScaleX;
        panel.Top = target.Y / dpi.DpiScaleY;
        void Position()
        {
            var handle = new WindowInteropHelper(panel).Handle;
            if (handle == 0 || !anchor.IsLoaded || !GetWindowRect(handle, out var bounds)) return;
            var width = bounds.Right - bounds.Left;
            var height = bounds.Bottom - bounds.Top;
            var topLeft = anchor.PointToScreen(new Point(0, 0));
            var bottomRight = anchor.PointToScreen(new Point(anchor.ActualWidth, anchor.ActualHeight));
            var spacing = (int)Math.Round(8 * dpi.DpiScaleY);
            int x = (int)(topLeft.X + bottomRight.X - width) / 2;
            int y = (int)topLeft.Y - height - spacing;
            if (y < work.Top + 3) y = (int)bottomRight.Y + spacing;
            x = Math.Clamp(x, work.Left + 3, Math.Max(work.Left + 3, work.Right - width - 3));
            y = Math.Clamp(y, work.Top + 3, Math.Max(work.Top + 3, work.Bottom - height - 3));
            SetWindowPos(handle, 0, x, y, 0, 0, 0x0215);
        }
        panel.SourceInitialized += (_,_) =>
        {
            var handle = new WindowInteropHelper(panel).Handle;
            var rounded = 2;
            _ = DwmSetWindowAttribute(handle, 33, ref rounded, 4);
            if (OperatingSystem.IsWindowsVersionAtLeast(10,0,22621))
            {
                var transient = 3;
                _ = DwmSetWindowAttribute(handle, 38, ref transient, 4);
            }
        };
        panel.ContentRendered += (_,_) => Position();
        panel.SizeChanged += (_,_) => Position();
        if (dismissOnBlur) panel.Deactivated += (_,_) => { if (panel.IsVisible) panel.Close(); };
    }

    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X,int Y);
    [StructLayout(LayoutKind.Sequential)] private struct RectNative { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public RectNative Bounds,Work; public int Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(NativePoint point,int flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd,out RectNative rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int cx,int cy,uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd,int attr,ref int value,int size);
}
