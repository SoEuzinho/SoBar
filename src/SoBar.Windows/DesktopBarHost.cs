using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SoBar.Windows;

public sealed class DesktopBarHost : IDisposable
{
    private const int EmergencyId = 0x6011;
    // All bars of a desktop session share a WPF dispatcher. Keep ownership on live HWNDs
    // so hotkeys survive removal of the monitor that registered them first.
    [ThreadStatic] private static List<DesktopBarHost>? activeHosts;
    private readonly Window window;
    private readonly Action emergencyExit;
    private readonly Action openCenter;
    private readonly DispatcherTimer timer;
    private readonly uint shellRestart = Native.RegisterWindowMessage("TaskbarCreated");
    private readonly uint appBarCallback = Native.RegisterWindowMessage("SoBar.DesktopBarHost.AppBar");
    private HwndSource? source;
    private nint hwnd;
    private bool registered, disposed, layoutPending, updating, hiddenForFullscreen;
    private int commandId = 0x6012;
    private bool commandRegistered;
    private ModifierKeys commandModifiers = ModifierKeys.Control | ModifierKeys.Alt;
    private Key commandKey = Key.Space;
    private string monitorId = "";
    private BarEdge edge = BarEdge.Top;
    private int thickness = 48;
    private double preferredLength;
    private bool reserve, autoHide, hideFullscreen = true;
    private DisplayInfo? display;
    private PixelRect placement;
    private PixelRect revealStrip, physicalEdgeStrip, expandedHoverArea;
    private PixelRect? applied;
    private (BarEdge Edge, PixelRect Rect)? reservation;
    private long lastHover;
    public bool EmergencyHotkeyRegistered { get; private set; }
    public bool CommandHotkeyRegistered => commandRegistered;
    public bool IsReserved => registered;

    public DesktopBarHost(Window window, Action emergencyExit, Action openCenter)
    {
        this.window = window;
        this.emergencyExit = emergencyExit;
        this.openCenter = openCenter;
        timer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher) { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += Tick;
        window.SourceInitialized += OnSourceInitialized;
        window.Closed += OnClosed;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != 0) Initialize(handle);
    }

    public static IReadOnlyList<DesktopMonitor> Monitors() =>
        DisplayInfo.ReadAll().OrderByDescending(m => m.Primary)
            .Select(m => new DesktopMonitor(m.Id, $"{m.Id.Replace(@"\\.\", "")}  ·  {m.Bounds.Width} × {m.Bounds.Height}", m.Primary)).ToList();

    public void Configure(string monitorId, string edge, bool reserve, bool autoHide, bool hideFullscreen, int thickness)
    {
        this.monitorId = monitorId ?? "";
        this.edge = Enum.TryParse<BarEdge>(edge, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : BarEdge.Top;
        this.reserve = reserve;
        this.autoHide = autoHide;
        this.hideFullscreen = hideFullscreen;
        this.thickness = thickness;
        if (hwnd != 0 && !disposed) Layout();
    }

    /// <summary>Sets the content length along the chosen edge in DIPs. Zero selects the full edge.</summary>
    public void SetPreferredLength(double lengthDip)
    {
        if (!double.IsFinite(lengthDip) || lengthDip < 0) throw new ArgumentOutOfRangeException(nameof(lengthDip));
        if (disposed || lengthDip == preferredLength) return;
        preferredLength = lengthDip;
        if (hwnd != 0) QueueLayout();
    }

    public bool SetCommandHotkey(ModifierKeys modifiers, Key key)
    {
        if (disposed || key == Key.None || modifiers == ModifierKeys.None) return false;
        if (commandRegistered && modifiers == commandModifiers && key == commandKey) return true;
        if (hwnd != 0)
        {
            if (!commandRegistered && activeHosts?.Any(h => h != this && h.commandRegistered) == true)
            {
                commandKey = key;
                commandModifiers = modifiers;
                return false;
            }
            int id = commandId == 0x6012 ? 0x6013 : 0x6012;
            if (!Native.RegisterHotKey(hwnd, id, (uint)modifiers | 0x4000, (uint)KeyInterop.VirtualKeyFromKey(key)))
                return false;
            if (commandRegistered) Native.UnregisterHotKey(hwnd, commandId);
            commandId = id;
            commandRegistered = true;
        }
        commandKey = key;
        commandModifiers = modifiers;
        return true;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => Initialize(new WindowInteropHelper(window).Handle);

    private void Initialize(nint handle)
    {
        if (hwnd != 0 || disposed) return;
        hwnd = handle;
        source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(Hook);
        (activeHosts ??= []).Add(this);
        AcquireHotkeys();
        Layout();
        // WPF applies its initial window placement after SourceInitialized.
        QueueLayout();
    }

    private void AcquireHotkeys()
    {
        if (!EmergencyHotkeyRegistered)
            EmergencyHotkeyRegistered = Native.RegisterHotKey(hwnd, EmergencyId, 0x4007, (uint)KeyInterop.VirtualKeyFromKey(Key.End));
        if (!commandRegistered) SetCommandHotkey(commandModifiers, commandKey);
    }

    private void QueueLayout()
    {
        if (layoutPending || disposed) return;
        layoutPending = true;
        window.Dispatcher.BeginInvoke(() =>
        {
            layoutPending = false;
            if (!disposed) Layout();
        }, DispatcherPriority.Loaded);
    }

    private Native.AppBarData Data() => new()
    {
        Size = Marshal.SizeOf<Native.AppBarData>(), Handle = hwnd, Message = appBarCallback, Edge = (uint)edge
    };

    private void ReleaseBar()
    {
        if (!registered) return;
        registered = false;
        reservation = null;
        var data = Data();
        Native.SHAppBarMessage(1, ref data); // ABM_REMOVE
    }

    private void Layout()
    {
        if (updating || hwnd == 0 || disposed) return;
        updating = true;
        try
        {
            // Autohide leaves the work area free and reveals a thin strip on the selected edge.
            bool shouldRegister = reserve && !autoHide;
            if (!shouldRegister) ReleaseBar();
            var displays = DisplayInfo.ReadAll();
            display = displays.FirstOrDefault(d => string.Equals(d.Id, monitorId, StringComparison.OrdinalIgnoreCase))
                ?? displays.FirstOrDefault(d => d.Primary) ?? displays.FirstOrDefault();
            if (display == null) return;
            if (shouldRegister && !registered)
            {
                var data = Data();
                registered = Native.SHAppBarMessage(0, ref data) != 0; // ABM_NEW
            }
            int inset = preferredLength == 0 ? 0 : (int)Math.Round(8 * display.Dpi / 96d);
            var stripSize = BarPlacement.PhysicalThickness(thickness, display.Dpi) + 2 * inset;
            var strip = BarPlacement.At(display.Work, edge, stripSize);
            if (registered)
            {
                var data = Data();
                data.Rect = BarPlacement.At(display.Bounds, edge, stripSize);
                Native.SHAppBarMessage(2, ref data); // ABM_QUERYPOS
                data.Rect = ResizeAfterQuery(data.Rect, stripSize);
                if (reservation != (edge, data.Rect))
                {
                    Native.SHAppBarMessage(3, ref data); // ABM_SETPOS
                    reservation = (edge, data.Rect);
                }
                strip = data.Rect;
            }
            placement = BarPlacement.FitDock(strip, edge, preferredLength, display.Dpi, inset);
            int revealPixels = Math.Max(2, (int)Math.Round(3 * display.Dpi / 96d));
            revealStrip = BarPlacement.EdgeStrip(strip, placement, edge, revealPixels);
            physicalEdgeStrip = BarPlacement.EdgeStrip(display.Bounds, placement, edge, revealPixels);
            expandedHoverArea = BarPlacement.HoverArea(placement, revealStrip);
            if (!Native.GetWindowRect(hwnd, out var actual) || actual != applied) applied = null;
            Tick(this, EventArgs.Empty);
            if (hideFullscreen || autoHide) timer.Start(); else timer.Stop();
        }
        finally { updating = false; }
    }

    // Explorer can shift either edge during negotiation; restore thickness towards the workspace.
    private PixelRect ResizeAfterQuery(PixelRect rect, int size) => edge switch
    {
        BarEdge.Top => rect with { Bottom = rect.Top + size },
        BarEdge.Bottom => rect with { Top = rect.Bottom - size },
        BarEdge.Left => rect with { Right = rect.Left + size },
        _ => rect with { Left = rect.Right - size }
    };

    internal static bool IsFullscreenWindow(nint foreground, DisplayInfo display)
    {
        if (foreground == 0 || Native.IsIconic(foreground) || Native.MonitorFromWindow(foreground, 2) != display.Handle) return false;
        Native.GetWindowThreadProcessId(foreground, out var pid);
        if (pid == Environment.ProcessId || DesktopWindowService.IsShellSurface(foreground)) return false;
        // A normally maximized application with a caption should keep access to the taskbar.
        if (Native.IsZoomed(foreground) && (Native.GetWindowLong(foreground, -16) & 0x00c00000) == 0x00c00000) return false;
        if (Native.GetFrame(foreground, 9, out var rect, Marshal.SizeOf<PixelRect>()) != 0 && !Native.GetWindowRect(foreground, out rect)) return false;
        return BarPlacement.Covers(rect, display.Bounds);
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (disposed || hwnd == 0 || display == null) return;
        bool fullscreen = hideFullscreen && IsFullscreenWindow(Native.GetForegroundWindow(), display);
        if (fullscreen)
        {
            if (!hiddenForFullscreen) Native.SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x0097); // HIDEWINDOW, NOACTIVATE, NOMOVE, NOSIZE, NOZORDER
            hiddenForFullscreen = true;
            return;
        }
        var target = placement;
        if (autoHide && Native.GetCursorPos(out var pointer))
        {
            var hitArea = applied == placement ? expandedHoverArea : revealStrip;
            if (hitArea.Contains(pointer.X, pointer.Y) || physicalEdgeStrip.Contains(pointer.X, pointer.Y) ||
                window.IsKeyboardFocusWithin || window.IsMouseCaptureWithin)
                lastHover = Environment.TickCount64;
            if (Environment.TickCount64 - lastHover > 700) target = revealStrip;
        }
        if (hiddenForFullscreen || target != applied)
        {
            uint flags = 0x0010; // SWP_NOACTIVATE
            if (hiddenForFullscreen) flags |= 0x0040; // SWP_SHOWWINDOW
            Native.SetWindowPos(hwnd, -1, target.Left, target.Top, target.Width, target.Height, flags);
            applied = target;
            hiddenForFullscreen = false;
        }
    }

    private nint Hook(nint handle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (disposed) return 0;
        if (message == shellRestart)
        {
            registered = false;
            reservation = null;
            QueueLayout();
        }
        else if (message == appBarCallback)
        {
            if (wParam == 1 && !updating) QueueLayout(); // ABN_POSCHANGED
        }
        else switch (message)
        {
            case 0x021: // WM_MOUSEACTIVATE — let a task click minimize the previous foreground window.
                handled = true;
                return 3;
            case 0x0312: // WM_HOTKEY
                if (EmergencyHotkeyRegistered && wParam == EmergencyId) { handled = true; window.Dispatcher.BeginInvoke(emergencyExit); }
                else if (commandRegistered && wParam == commandId) { handled = true; window.Dispatcher.BeginInvoke(openCenter); }
                break;
            case 0x007e: // WM_DISPLAYCHANGE
            case 0x001a: // WM_SETTINGCHANGE
            case 0x02e0: // WM_DPICHANGED
                QueueLayout();
                break;
            case 0x0006: // WM_ACTIVATE
            case 0x0047: // WM_WINDOWPOSCHANGED
                if (registered)
                {
                    var data = Data();
                    Native.SHAppBarMessage(message == 6 ? 6u : 9u, ref data);
                }
                break;
            case 0x0002: // WM_DESTROY
                Dispose();
                break;
        }
        return 0;
    }

    private void OnClosed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Stop();
        timer.Tick -= Tick;
        bool transferCommand = commandRegistered;
        bool ownedHotkeys = transferCommand || EmergencyHotkeyRegistered;
        if (commandRegistered) Native.UnregisterHotKey(hwnd, commandId);
        if (EmergencyHotkeyRegistered) Native.UnregisterHotKey(hwnd, EmergencyId);
        commandRegistered = EmergencyHotkeyRegistered = false;
        activeHosts?.Remove(this);
        if (ownedHotkeys && activeHosts != null)
        {
            foreach (var remaining in activeHosts)
            {
                if (transferCommand && !remaining.commandRegistered)
                {
                    remaining.commandModifiers = commandModifiers;
                    remaining.commandKey = commandKey;
                }
                remaining.AcquireHotkeys();
            }
        }
        ReleaseBar();
        source?.RemoveHook(Hook);
        window.SourceInitialized -= OnSourceInitialized;
        window.Closed -= OnClosed;
        hwnd = 0;
    }
}

