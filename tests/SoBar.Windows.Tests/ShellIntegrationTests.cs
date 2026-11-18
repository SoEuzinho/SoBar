using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Threading;
using Xunit;

namespace SoBar.Windows.Tests;

// Opt in on an interactive workstation because these tests temporarily reserve desktop space
// and create a disposable window in a second process. They never operate on the user's apps.
internal class InteractiveShellFactAttribute : FactAttribute
{
    public InteractiveShellFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOBAR_NATIVE_SMOKE") != "1")
            Skip = "Set SOBAR_NATIVE_SMOKE=1 to exercise the interactive Windows shell.";
    }
}

internal sealed class MultiDisplayFactAttribute : InteractiveShellFactAttribute
{
    public MultiDisplayFactAttribute()
    {
        if (Skip == null && DisplayInfo.ReadAll().Count < 2)
            Skip = "Two active Windows desktop monitors are required.";
    }
}

[CollectionDefinition("Windows desktop", DisableParallelization = true)]
public sealed class WindowsDesktopCollection { }

[Collection("Windows desktop")]
public sealed class ShellIntegrationTests
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(nint hwnd, int message, nint wParam, nint lParam);

    [MultiDisplayFact]
    public void AssignsTwoRealDisplaysAndTransfersHotkeysWhenFirstBarCloses()
    {
        DesktopTests.RunSta(() =>
        {
            var before = DisplayInfo.ReadAll();
            var primary = before.Single(m => m.Primary);
            var secondary = before.First(m => !m.Primary);
            var first = NewOverlay();
            var second = NewOverlay();
            int firstExit = 0, secondExit = 0;
            using var firstHost = new DesktopBarHost(first, () => firstExit++, () => { });
            using var secondHost = new DesktopBarHost(second, () => secondExit++, () => { });
            void CheckArea()
            {
                var current = DisplayInfo.ReadAll();
                foreach (var monitor in before)
                    Assert.Equal(monitor.Work, current.Single(m => m.Id == monitor.Id).Work);
            }
            try
            {
                firstHost.Configure(primary.Id, "Top", false, false, false, 40);
                secondHost.Configure(secondary.Id, "Bottom", false, false, false, 50);
                first.Show();
                Assert.True(firstHost.SetCommandHotkey(ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.F9));
                second.Show();
                var firstHwnd = new WindowInteropHelper(first).Handle;
                var secondHwnd = new WindowInteropHelper(second).Handle;
                first.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Equal(primary.Handle, Native.MonitorFromWindow(firstHwnd, 0));
                Assert.Equal(secondary.Handle, Native.MonitorFromWindow(secondHwnd, 0));
                Assert.True(Native.GetWindowRect(firstHwnd, out var firstRect));
                Assert.True(Native.GetWindowRect(secondHwnd, out var secondRect));
                Assert.Equal(BarPlacement.At(primary.Work, BarEdge.Top, BarPlacement.PhysicalThickness(40, primary.Dpi)), firstRect);
                Assert.Equal(BarPlacement.At(secondary.Work, BarEdge.Bottom, BarPlacement.PhysicalThickness(50, secondary.Dpi)), secondRect);
                CheckArea();
                Assert.True(firstHost.EmergencyHotkeyRegistered);
                Assert.False(secondHost.EmergencyHotkeyRegistered);
                Assert.False(secondHost.CommandHotkeyRegistered);
                // Send only to the test HWND. OS registration and callback delivery are checked separately.
                Assert.True(PostMessage(firstHwnd, 0x0312, 0x6011, 0));
                PumpUntil(() => firstExit == 1);
                first.Close();
                PumpUntil(() => secondHost.EmergencyHotkeyRegistered && secondHost.CommandHotkeyRegistered);
                Assert.True(PostMessage(secondHwnd, 0x0312, 0x6011, 0));
                PumpUntil(() => secondExit == 1);
                Assert.Equal(secondary.Handle, Native.MonitorFromWindow(secondHwnd, 0));
                CheckArea();
            }
            finally
            {
                firstHost.Dispose(); secondHost.Dispose();
                if (first.IsLoaded) first.Close();
                if (second.IsLoaded) second.Close();
            }
            CheckArea();
        });
    }

    [InteractiveShellFact]
    public void AppBarReservesAnEdgeThenReleasesIt()
    {
        DesktopTests.RunSta(() =>
        {
            var screen = DisplayInfo.ReadAll().Single(d => d.Primary);
            var window = NewOverlay();
            using var host = new DesktopBarHost(window, () => { }, () => { });
            try
            {
                host.Configure(screen.Id, "Top", true, false, false, 30);
                window.Show();
                PumpUntil(() => DisplayInfo.ReadAll().Single(d => d.Primary).Work.Top > screen.Work.Top);
                Assert.True(host.IsReserved);
                var taken = DisplayInfo.ReadAll().Single(d => d.Primary).Work;
                Assert.Equal(BarPlacement.PhysicalThickness(30, screen.Dpi), taken.Top - screen.Work.Top);
            }
            finally { host.Dispose(); window.Close(); }
            PumpUntil(() => DisplayInfo.ReadAll().Single(d => d.Primary).Work == screen.Work);
        });
    }

    [InteractiveShellFact]
    public void TracksForeignWindowAndRendersItsDwmPreview()
    {
        DesktopTests.RunSta(() =>
        {
            string title = "SoBar QA " + Guid.NewGuid().ToString("N");
            string script = $$"""
                Add-Type -AssemblyName System.Windows.Forms
                $form = New-Object Windows.Forms.Form
                $form.Text = '{{title}}'
                $form.FormBorderStyle = 'None'
                $form.Width = 320
                $form.Height = 180
                [Windows.Forms.Application]::Run($form)
                """;
            var info = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
                UseShellExecute = false, CreateNoWindow = true
            };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-STA", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) })
                info.ArgumentList.Add(arg);
            using var child = Process.Start(info)!;
            var owner = NewOverlay();
            try
            {
                var service = new DesktopWindowService();
                DesktopWindow? task = null;
                PumpUntil(() => (task = service.List().FirstOrDefault(w => w.Title == title)) != null);
                Assert.NotNull(task);
                Assert.Equal(info.FileName, task.ExecutablePath, ignoreCase: true);
                Assert.NotNull(task.Icon);
                var screen = DisplayInfo.ReadAll().Single(m => m.Id == task.MonitorId);
                Assert.False(DesktopBarHost.IsFullscreenWindow(task.Handle, screen));
                var r = screen.Bounds;
                Assert.True(Native.SetWindowPos(task.Handle, 0, r.Left, r.Top, r.Width, r.Height, 0x14));
                Assert.True(DesktopBarHost.IsFullscreenWindow(task.Handle, screen));
                Assert.True(Native.SetWindowPos(task.Handle, 0, screen.Work.Left + 80, screen.Work.Top + 80, 320, 180, 0x14));
                owner.Show();
                WindowPreview.Show(owner, task.Handle);
                var preview = Assert.Single(owner.OwnedWindows.Cast<Window>());
                var fallback = Assert.IsType<TextBlock>(Assert.IsType<Border>(preview.Content).Child);
                PumpUntil(() => fallback.Visibility == Visibility.Collapsed);
                preview.Close();
            }
            finally
            {
                owner.Close();
                if (!child.HasExited)
                {
                    child.CloseMainWindow();
                    if (!child.WaitForExit(1500)) child.Kill();
                    child.WaitForExit(2000);
                }
            }
        });
    }

    private static Window NewOverlay() => new() { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
        ShowInTaskbar = false, ShowActivated = false, Width = 260, Height = 32 };

    private static void PumpUntil(Func<bool> predicate)
    {
        var elapsed = Stopwatch.StartNew();
        while (!predicate() && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Thread.Sleep(50);
        }
        Assert.True(predicate(), "The interactive shell did not reach the expected state.");
    }
}

