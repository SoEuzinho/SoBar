using System.IO;
using System.Windows;
using System.Windows.Threading;
using SoBar.Core;
using SoBar.Infrastructure;
using SoBar.Widgets;
using SoBar.Windows;

namespace SoBar.App;

internal sealed partial class Shell : IDisposable
{
    public AppServices Services { get; }
    public WidgetService Widgets { get; }
    public DesktopWindowService Desktop { get; }
    public Action Exit { get; }
    private readonly List<BarWindow> _bars = [];
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(1.25) };
    private CenterWindow? _center;
    private SettingsWindow? _settings;
    private int _samples;
    private string _monitorSignature = "";
    private IReadOnlyList<PluginTile> _plugins = [];
    private IReadOnlyList<DesktopWindow> _windows = [];
    private readonly Dictionary<BarWindow, AppSettings> _applied = [];
    private ContextProfiles? _contexts;
    private bool _refreshing;
    private bool _disposed;
    public Shell(AppServices services, WidgetService widgets, DesktopWindowService desktop, Action exit)
    {
        Services = services;
        Widgets = widgets;
        Desktop = desktop;
        Exit = exit;
        _poll.Tick += async (_, _) => await RefreshAsync();
    }
    public void Start()
    {
        Reconfigure();
        _ = RefreshAsync();
        _poll.Start();
    }
    public void Reconfigure()
    {
        _contexts = Services.ReadContextProfiles();
        _applied.Clear();
        var s = _contexts.Default;
        Presentation.Apply(s);
        var monitors = DesktopBarHost.Monitors();
        _monitorSignature = string.Join("|", monitors.Select(x => x.Id));
        var ids = s.AllMonitors ? monitors.Select(x => x.Id).ToArray() :
            [monitors.Any(x => x.Id == s.MonitorId) ? s.MonitorId : monitors.FirstOrDefault()?.Id ?? ""];
        if (ids.Length == 0) ids = [""];
        foreach (var bar in _bars.Where(b => !ids.Contains(b.MonitorId)).ToArray()) { bar.Close(); _bars.Remove(bar); }
        _plugins = Services.PluginTiles();
        foreach (var id in ids)
        {
            var bar = _bars.FirstOrDefault(b => b.MonitorId == id);
            if (bar == null)
            {
                bar = new BarWindow(this, id);
                _bars.Add(bar);
                bar.Show();
            }
        }
        ApplyToBars();
    }
    private async Task RefreshAsync()
    {
        if (_disposed || _refreshing) return;
        _refreshing = true;
        try
        {
            var windows = await Task.Run(Desktop.List);
            if (_disposed) return;
            _windows = windows;
            if (++_samples % 8 == 0)
            {
                if (_monitorSignature != string.Join("|", DesktopBarHost.Monitors().Select(x => x.Id)))
                {
                    Reconfigure();
                    return;
                }
                if (_plugins.Count != 0)
                {
                    _plugins = Services.PluginTiles();
                    foreach (var bar in _bars) bar.UpdatePlugins(_plugins);
                }
            }
            ApplyToBars();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (!_disposed) Services.Log("Falha ao atualizar a barra: " + error.GetType().Name);
        }
        finally { _refreshing = false; }
    }
    private void ApplyToBars()
    {
        if (_contexts == null) return;
        var foreground = _windows.FirstOrDefault(w => w.IsActive);
        foreach (var bar in _bars)
        {
            var settings = _contexts.Resolve(bar.MonitorId, foreground?.MonitorId, foreground?.ExecutablePath);
            if (!_applied.TryGetValue(bar, out var previous) || !ReferenceEquals(previous, settings))
            {
                bar.Configure(settings, _plugins);
                _applied[bar] = settings;
            }
            bar.UpdateApps(_windows);
        }
    }
    public bool Execute(Action action)
    {
        try { action(); return true; }
        catch (Exception error)
        {
            Services.Log(error.GetType().Name + ": " + error.Message);
            MessageBox.Show(error.Message, "SoBar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
    public string ShortcutStatus => (_bars.Any(b => b.RequestedShortcutAvailable) ? "Central • " + Services.Settings.Shortcut : "Atalho da central indisponível") +
        (_bars.Any(b => b.EmergencyAvailable) ? "   |   Saída • Ctrl+Alt+Shift+End" : "   |   Saída • menu SoBar");
    public void OpenCenter()
    {
        if (_center == null)
        {
            _center = new CenterWindow(this);
            _center.Closed += (_, _) => _center = null;
        }
        _center.Show(); _center.Activate(); _center.FocusQuery();
    }
    public void OpenSettings()
    {
        if (_settings == null)
        {
            _settings = new SettingsWindow(this);
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show(); _settings.Activate();
    }
    public void TogglePin(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        var s = Services.Settings;
        var pinned = s.PinnedApps.FirstOrDefault(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
        if (pinned != null) s.PinnedApps.Remove(pinned); else s.PinnedApps.Add(path);
        Execute(() => { Services.Save(s); Reconfigure(); });
    }
    public async Task VerifySmokeAsync(string? capturePath)
    {
        var bars = _bars.Count;
        VerifyContextProfile();
        if (bars == 0 || _bars.Any(x => !x.IsLoaded || x.ActualHeight < 1 || !x.HasClock)) throw new InvalidOperationException("Barra ou relógio ausente");
        if (_bars.Any(bar => !TimeOnly.TryParse(bar.ClockText, out _))) throw new InvalidOperationException("O relógio não forneceu um horário.");
        foreach (var key in Application.Current.Resources.Keys) { try { _ = Application.Current.Resources[key]; } catch (Exception e) { throw new InvalidOperationException(key.ToString(),e); } }
        OpenCenter();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (_center?.IsLoaded != true) throw new InvalidOperationException("Central indisponível");
        _center.Close();
        OpenSettings();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (_settings?.IsLoaded != true) throw new InvalidOperationException("Configurações indisponíveis");
        if (capturePath != null)
        {
            Capture(_settings, capturePath);

        }
        _settings.Width = 390;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        _settings.VerifyPages();
        _settings.Close();
        if (capturePath != null) await CaptureDockAsync(Path.GetDirectoryName(Path.GetFullPath(capturePath))!);
        var defaults = Services.Settings;
        foreach (var (theme, edge, size) in new[] { ("Midnight", "Left", "Grande"), ("Liquid Glass", "Right", "Normal"), ("iOS Glass", "Top", "Compacto"), ("Claro", "Bottom", "Normal") })
        {
            var trial = Services.Settings;
            trial.Theme = theme; trial.Edge = edge; trial.Size = size;
            trial.Widgets = ["clock-card","cpu","ram"];
            trial.WidgetScales["clock-card"] = 1.25;
            trial.Widgets.Reverse();
            trial.WidgetGroups["cpu"] = trial.WidgetGroups["ram"] = "Sistema";
            Services.Save(trial); Reconfigure();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            if (_bars.Any(b => !b.HasClock || !b.IsLoaded || !b.VisibleGroupNames.SequenceEqual(["Sistema"]))) throw new InvalidOperationException("Falha no layout " + theme);
        }
        Services.Save(defaults); Reconfigure();
        if (_bars.Any(b => b.VisibleGroupNames.Length != 0)) throw new InvalidOperationException("Grupo persistiu após restauração");
        var manifest = Path.Combine(Services.DataDirectory, "smoke-extension.json");
        File.WriteAllText(manifest, """{"apiVersion":1,"id":"smoke","name":"Teste","source":"text","permissions":[],"text":"Verificado"}""");
        Services.PreviewPlugin(manifest);
        Services.ImportPlugin(manifest);
        if (Services.PluginTiles().Single(x => x.Id == "smoke").Value != "Verificado") throw new InvalidDataException("Extensão indisponível");
        Services.Log($"Smoke concluído em {bars} monitor(es).");
    }
    private static void Capture(Window panel, string path)
    {
        var surface = (FrameworkElement)panel.Content;
        surface.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth), (int)Math.Ceiling(surface.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        var visual = new System.Windows.Media.DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, surface.ActualWidth, surface.ActualHeight);
            drawing.DrawRectangle(panel.Background, null, bounds);
            drawing.DrawRectangle(new System.Windows.Media.VisualBrush(surface), null, bounds);
        }
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _poll.Stop();
        foreach (var bar in _bars.ToArray()) { if (bar.IsLoaded) bar.Close(); }
        _bars.Clear();
    }
}
