using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SoBar.App;

internal sealed partial class Shell
{
    // The caller has selected --smoke-test --capture-settings and uses a temporary database.
    private async Task CaptureDockAsync(string destination)
    {
        _poll.Stop();
        while (_refreshing) await Task.Delay(25);
        _windows = [];
        var original = Services.Settings;
        var demo = Services.Settings;
        var os = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system = Environment.SystemDirectory;
        demo.PinnedApps = new[] { Path.Combine(os,"explorer.exe"), Path.Combine(system,"notepad.exe"),
            Path.Combine(system,"Taskmgr.exe"), Path.Combine(system,"calc.exe"), Path.Combine(system,"charmap.exe"),
            Path.Combine(system,"WindowsPowerShell","v1.0","powershell.exe") }.Where(File.Exists).ToList();
        demo.Widgets = ["system-card","agenda","power-card","clock-card"];
        demo.WidgetGroups.Clear();
        Window? backdrop = null, callout = null;
        try
        {
            Services.Save(demo); Reconfigure();
            var dock = _bars[0];
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Task.Delay(4500);
            var barHandle = new WindowInteropHelper(dock).Handle;
            var bar = VisibleArea(dock);
            var bgArea = new Int32Rect(bar.X-30,bar.Y-420,bar.Width+60,bar.Height+450);
            backdrop = new Window { Title = "SoBar · Fundo de validação", Width = 10, Height = 10, WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false, Topmost = true,
                Background = TestBackdrop(), WindowStartupLocation = WindowStartupLocation.Manual };
            backdrop.Show();
            var bg = new WindowInteropHelper(backdrop).Handle;
            if (!SetWindowPos(bg,new nint(-1),bgArea.X,bgArea.Y,bgArea.Width,bgArea.Height,0x0010) ||
                !SetWindowPos(barHandle,new nint(-1),0,0,0,0,0x0013)) throw new IOException("Falha ao posicionar o fundo de validação.");
            await Task.Delay(380);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            bar = VisibleArea(dock);
            var testArea = Expand(bar,8);
            var graph = backdrop.Background;
            async Task<int[]> Sample(Brush color)
            {
                backdrop.Background = color;
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                await Task.Delay(250);
                DwmFlush(); EnsureOwnedScene(testArea,bg,[barHandle]);
                var screen = GetDC(0);
                try
                {
                    int x = bar.X + bar.Width / 2;
                    return [GetPixel(screen,x,bar.Y-4), GetPixel(screen,x,bar.Y+4), GetPixel(screen,x,bar.Y+bar.Height-5)];
                }
                finally { ReleaseDC(0,screen); }
            }
            var dark = await Sample(Brushes.Black);
            var light = await Sample(Brushes.White);
            Console.WriteLine("Vidro (externo/superior/inferior, BGR): " + string.Join("/",dark.Select(x => x.ToString("X6"))) + " -> " + string.Join("/",light.Select(x => x.ToString("X6"))));
            backdrop.Background = graph;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); await Task.Delay(250);
            Snapshot(testArea,bg,[barHandle],Path.Combine(destination,"barra.png"));
            dock.OpenWidget("agenda");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Task.Delay(350);
            callout = Application.Current.Windows.Cast<Window>().Single(w => w.Owner == dock && w.Title.Contains("Agenda",StringComparison.OrdinalIgnoreCase));
            var card = VisibleArea(callout);
            var x = Math.Min(bar.X,card.X);
            var y = Math.Min(bar.Y,card.Y);
            var rect = new Int32Rect(x,y,Math.Max(bar.X+bar.Width,card.X+card.Width)-x,Math.Max(bar.Y+bar.Height,card.Y+card.Height)-y);
            Snapshot(Expand(rect,8),bg,[barHandle,new WindowInteropHelper(callout).Handle],Path.Combine(destination,"dock-contexto.png"));
        }
        finally
        {
            callout?.Close(); backdrop?.Close();
            Services.Save(original); Reconfigure();
            _poll.Start();
        }
    }
    private static Brush TestBackdrop()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            LinearGradientBrush Gradient(string a,string b,double angle) => new((Color)ColorConverter.ConvertFromString(a),(Color)ColorConverter.ConvertFromString(b),angle);
            dc.DrawRectangle(Gradient("#1A2332","#78909C",16),null,new Rect(0,0,1100,460));
            dc.DrawGeometry(Gradient("#27323D","#97A9AD",78),null,Geometry.Parse("M 0,344 C 240,363 320,44 602,61 C 846,76 957,322 1100,262 L 1100,460 0,460 Z"));
            dc.DrawGeometry(Gradient("#222934","#5D707C",112),null,Geometry.Parse("M 0,409 C 310,467 380,178 645,214 S 886,503 1100,343 L 1100,460 0,460 Z"));
            var tones = new[] { "#CA825D","#417587","#789FA2","#69719B" };
            for (int i = 0; i < tones.Length; i++)
            {
                var stripe = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tones[i])) { Opacity=.52 };
                dc.DrawRectangle(stripe,null,new Rect(100+245*i,0,75,460));
            }
        }
        var brush = new DrawingBrush(drawing) { Stretch = Stretch.Fill }; brush.Freeze(); return brush;
    }
    private static Int32Rect VisibleArea(Window w)
    {
        var p = w.PointToScreen(new Point());
        var end = w.PointToScreen(new Point(w.ActualWidth,w.ActualHeight));
        return new((int)Math.Floor(p.X),(int)Math.Floor(p.Y),(int)Math.Ceiling(end.X-p.X),(int)Math.Ceiling(end.Y-p.Y));
    }
    private static Int32Rect Expand(Int32Rect r,int pad) => new(r.X-pad,r.Y-pad,r.Width+2*pad,r.Height+2*pad);
    [StructLayout(LayoutKind.Sequential)] private struct ScreenRect { public int Left,Top,Right,Bottom; }
    private delegate bool Enumerate(nint window,nint data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Enumerate callback,nint data);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window,out ScreenRect rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window,int attribute,out int value,int length);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window,nint insertAfter,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window,nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc,int width,int height);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc,nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern int GetPixel(nint dc,int x,int y);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint dest,int x,int y,int width,int height,nint src,int sx,int sy,uint operation);
    private static void EnsureOwnedScene(Int32Rect area,nint background,nint[] allowed)
    {
        var complete = false;
        EnumWindows((window,_) => {
            if (window == background)
            {
                complete = GetWindowRect(background,out var rect) && rect.Left <= area.X && rect.Top <= area.Y &&
                    rect.Right >= area.X+area.Width && rect.Bottom >= area.Y+area.Height;
                return false;
            }
            if (allowed.Contains(window) || !IsWindowVisible(window) ||
                (DwmGetWindowAttribute(window,14,out var cloaked,sizeof(int)) == 0 && cloaked != 0)) return true;
            if (!GetWindowRect(window,out var other)) return false;
            return other.Left >= area.X+area.Width || other.Right <= area.X || other.Top >= area.Y+area.Height || other.Bottom <= area.Y;
        },0);
        if (!complete) throw new IOException("A cena de teste não está totalmente coberta pelas janelas do SoBar.");
    }
    private static void Snapshot(Int32Rect area,nint backdrop,nint[] allowed,string path)
    {
        DwmFlush();
        EnsureOwnedScene(area,backdrop,allowed);
        var screen=GetDC(0); var memory=CreateCompatibleDC(screen);
        var bitmap=CreateCompatibleBitmap(screen,area.Width,area.Height);
        var previous=SelectObject(memory,bitmap);
        try
        {
            if (screen == 0 || memory == 0 || bitmap == 0 || !BitBlt(memory,0,0,area.Width,area.Height,screen,area.X,area.Y,0x40CC0020))
                throw new IOException("A captura do compositor falhou.");
            EnsureOwnedScene(area,backdrop,allowed);
            SelectObject(memory,previous); previous=0;
            var pixels=Imaging.CreateBitmapSourceFromHBitmap(bitmap,0,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
            var opaque=new FormatConvertedBitmap(pixels,PixelFormats.Bgr24,null,0);
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(opaque));
            using var file=File.Create(path); encoder.Save(file);
        }
        finally
        {
            if (previous != 0) SelectObject(memory,previous);
            if (bitmap != 0) DeleteObject(bitmap);
            if (memory != 0) DeleteDC(memory);
            if (screen != 0) ReleaseDC(0,screen);
        }
    }
}
