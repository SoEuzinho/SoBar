using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace SoBar.Windows;

public static class WindowPreview
{
    private static Preview? current;

    public static void Show(Window owner, nint handle)
    {
        current?.Close();
        if (!Native.IsWindow(handle)) return;
        current = new Preview(owner, handle);
        current.Closed += (_, _) => current = null;
        current.Show();
    }

    private sealed class Preview : Window
    {
        private readonly nint source;
        private nint thumbnail;
        private readonly TextBlock status;
        private readonly Border canvas;

        public Preview(Window owner, nint source)
        {
            this.source = source;
            Owner = owner;
            Title = DesktopWindowService.TitleOf(source);
            Width = 342;
            Height = 220;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Background = new SolidColorBrush(Color.FromRgb(25, 29, 34));
            status = new TextBlock
            {
                Text = "Prévia indisponível para esta janela.",
                Foreground = Brushes.WhiteSmoke,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            canvas = new Border { Margin = new Thickness(8), Child = status };
            Content = canvas;
            ContentRendered += (_, _) => Connect();
            canvas.MouseLeftButtonUp += (_, _) => { new DesktopWindowService().Activate(source); Close(); };
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
            Deactivated += (_, _) => { if (IsVisible) Close(); };
            SourceInitialized += (_, _) => Position(owner);
            Closed += (_, _) =>
            {
                if (thumbnail != 0) Native.DwmUnregisterThumbnail(thumbnail);
                thumbnail = 0;
            };
        }

        private void Position(Window owner)
        {
            var hwnd = new WindowInteropHelper(owner).Handle;
            var monitor = Native.MonitorFromWindow(hwnd, 2);
            var display = DisplayInfo.ReadAll().FirstOrDefault(m => m.Handle == monitor);
            if (display == null || !Native.GetWindowRect(hwnd, out var bar)) return;
            Native.GetCursorPos(out var pointer);
            var scale = display.Dpi / 96d;
            int width = (int)(Width * scale), height = (int)(Height * scale);
            int x = pointer.X - width / 2, y = bar.Bottom + 6;
            if (bar.Width < bar.Height)
            {
                x = bar.Left - width - 6 >= display.Work.Left ? bar.Left - width - 6 : bar.Right + 6;
                y = pointer.Y - height / 2;
            }
            else if (y + height > display.Work.Bottom) y = bar.Top - height - 6;
            x = Math.Clamp(x, display.Work.Left, Math.Max(display.Work.Left, display.Work.Right - width));
            y = Math.Clamp(y, display.Work.Top, Math.Max(display.Work.Top, display.Work.Bottom - height));
            Native.SetWindowPos(new WindowInteropHelper(this).Handle, 0, x, y, width, height, 0x14);
        }

        private void Connect()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == 0 || Native.DwmRegisterThumbnail(hwnd, source, out thumbnail) != 0 || thumbnail == 0) return;
            if (Native.DwmQueryThumbnailSourceSize(thumbnail, out var size) != 0 || size.X <= 0 || size.Y <= 0 ||
                !Native.GetClientRect(hwnd, out var client)) return;
            int margin = Math.Max(4, (int)(8 * VisualTreeHelper.GetDpi(this).DpiScaleX));
            double scale = Math.Min((client.Width - 2d * margin) / size.X, (client.Height - 2d * margin) / size.Y);
            if (scale <= 0) return;
            int width = Math.Max(1, (int)(size.X * scale)), height = Math.Max(1, (int)(size.Y * scale));
            int left = (client.Width - width) / 2, top = (client.Height - height) / 2;
            var properties = new Native.ThumbnailProperties
            {
                Flags = 1 | 4 | 8, Destination = new(left, top, left + width, top + height), Opacity = 255, Visible = 1
            };
            if (Native.DwmUpdateThumbnailProperties(thumbnail, ref properties) == 0) status.Visibility = Visibility.Collapsed;
        }
    }
}

