using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Xunit;

namespace SoBar.Windows.Tests;

public class DesktopTests
{
    [Theory]
    [InlineData("Top", -1920, -200, 0, -140)]
    [InlineData("Bottom", -1920, 820, 0, 880)]
    [InlineData("Left", -1920, -200, -1860, 880)]
    [InlineData("Right", -60, -200, 0, 880)]
    public void PositionsBarOnMonitorWithNegativeOrigin(string edge, int x, int y, int right, int bottom)
    {
        var actual = BarPlacement.At(new PixelRect(-1920, -200, 0, 880), Enum.Parse<BarEdge>(edge), 60);
        Assert.Equal(new PixelRect(x, y, right, bottom), actual);
    }

    [Theory]
    [InlineData(96, 48)]
    [InlineData(120, 60)]
    [InlineData(144, 72)]
    [InlineData(192, 96)]
    public void ConvertsLogicalHeightToPhysicalPixels(int dpi, int expected)
        => Assert.Equal(expected, BarPlacement.PhysicalThickness(48, (uint)dpi));

    [Fact]
    public void FullscreenMustCoverEveryMonitorEdge()
    {
        var screen = new PixelRect(100, -50, 1500, 980);
        Assert.True(BarPlacement.Covers(screen, screen));
        Assert.True(BarPlacement.Covers(new(99, -50, 1501, 979), screen));
        Assert.False(BarPlacement.Covers(screen with { Bottom = 932 }, screen));
        Assert.False(BarPlacement.Covers(screen with { Left = 1500 }, screen));
    }

    [Theory]
    [InlineData("Top", -1185, -188, -735, -116)]
    [InlineData("Bottom", -1185, 796, -735, 868)]
    [InlineData("Left", -1908, 115, -1836, 565)]
    [InlineData("Right", -83, 115, -11, 565)]
    public void FitsFloatingContentAt150PercentWithNegativeOrigins(string position, int left, int top, int right, int bottom)
    {
        var edge = Enum.Parse<BarEdge>(position);
        var bounds = new PixelRect(-1920, -200, 1, 880);
        var strip = BarPlacement.At(bounds, edge, 72 + 24);
        var dock = BarPlacement.FitDock(strip, edge, 300, 144, 12);
        Assert.Equal(new PixelRect(left, top, right, bottom), dock);
    }

    [Fact]
    public void ClampsLongDocksAndKeepsShellEdgeReachableWithStockTaskbar()
    {
        var bounds = new PixelRect(-1360, 312, 0, 1080);
        var work = bounds with { Bottom = 1032 };
        var strip = BarPlacement.At(work, BarEdge.Bottom, 64);
        var dock = BarPlacement.FitDock(strip, BarEdge.Bottom, 9000, 96, 8);
        Assert.Equal(new PixelRect(-1352, 976, -8, 1024), dock);
        var reveal = BarPlacement.EdgeStrip(work, dock, BarEdge.Bottom, 3);
        var screenEdge = BarPlacement.EdgeStrip(bounds, dock, BarEdge.Bottom, 3);
        Assert.Equal(new PixelRect(-1352, 1029, -8, 1032), reveal);
        Assert.True(screenEdge.Contains(-680, 1079));
        Assert.False(screenEdge.Contains(-680, 1050));
        Assert.True(BarPlacement.HoverArea(dock, reveal).Contains(-680, 1026));
        Assert.Equal(strip, BarPlacement.FitDock(strip, BarEdge.Bottom, 0, 96, 0));
    }

    [Fact]
    public void EnumeratesPhysicalDisplayAndLiveTaskList()
    {
        var displays = DesktopBarHost.Monitors();
        Assert.Contains(displays, d => d.IsPrimary);
        Assert.Equal(displays.Count, displays.Select(d => d.Id).Distinct().Count());
        var tasks = new DesktopWindowService();
        foreach (var task in tasks.List())
        {
            Assert.NotEqual(0, task.Handle);
            Assert.False(string.IsNullOrWhiteSpace(task.Title));
            Assert.Contains(displays, d => d.Id == task.MonitorId);
            Assert.True(task.Icon == null || task.Icon.IsFrozen);
        }
        Assert.False(tasks.Activate(0));
        Assert.False(tasks.Toggle(0));
    }

    [Fact]
    public void ReadsAndCopiesARealShellIcon()
    {
        var icon = DesktopWindowService.IconForPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
        Assert.NotNull(icon);
        Assert.True(icon.IsFrozen);
    }

    [Fact]
    public void WindowHostPositionsANonActivatingTestOverlayAndLeavesWorkAreaAvailable()
    {
        RunSta(() =>
        {
            var primary = DisplayInfo.ReadAll().Single(m => m.Primary);
            var overlay = new Window { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false, ShowActivated = false, Width = 240, Height = 42 };
            using var host = new DesktopBarHost(overlay, () => { }, () => { });
            try
            {
                host.Configure(primary.Id, "Top", false, false, false, 42);
                overlay.Show();
                overlay.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(Native.GetWindowRect(new WindowInteropHelper(overlay).Handle, out var actual));
                Assert.Equal(BarPlacement.At(primary.Work, BarEdge.Top, BarPlacement.PhysicalThickness(42, primary.Dpi)), actual);
                Assert.False(host.IsReserved);
                Assert.Equal(primary.Work, DisplayInfo.ReadAll().Single(m => m.Primary).Work);
            }
            finally { host.Dispose(); overlay.Close(); }
        });
    }

    internal static void RunSta(Action body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { body(); } catch (Exception ex) { error = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "STA dispatcher exceeded timeout");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}

