using System.IO;
using System.Windows;
using System.Windows.Threading;
using SoBar.Core;
using SoBar.Infrastructure;
using SoBar.Widgets;
using SoBar.Windows;

namespace SoBar.App;

internal sealed class Shell : IDisposable
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
    private bool _disposed;
    public Shell(AppServices services, WidgetService widgets, DesktopWindowService desktop, Action exit)
    {
        Services = services;
        Widgets = widgets;
        Desktop = desktop;
        Exit = exit;
        _poll.Tick += (_, _) => Refresh();
    }
    public void Start()
    {
        Reconfigure();
        Refresh();
        _poll.Start();
    }
    public void Reconfigure()
    {
        var s = Services.Settings;
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
            bar.Configure(s, _plugins);
        }
        Refresh();
    }
    private void Refresh()
    {
        if (_disposed) return;
        if (++_samples % 8 == 0 && _monitorSignature != string.Join("|", DesktopBarHost.Monitors().Select(x => x.Id)))
        {
            Reconfigure();
            return;
        }
        if (_plugins.Count != 0 && _samples % 8 == 0)
        {
            _plugins = Services.PluginTiles();
            foreach (var bar in _bars) bar.UpdatePlugins(_plugins);
        }
        var windows = Desktop.List();
        foreach (var bar in _bars) bar.UpdateApps(windows);
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
        if (bars == 0 || _bars.Any(x => !x.IsLoaded || x.ActualHeight < 1 || !x.HasClock)) throw new InvalidOperationException("Barra ou relógio ausente");
        foreach (var key in Application.Current.Resources.Keys) { try { _ = Application.Current.Resources[key]; } catch (Exception e) { throw new InvalidOperationException(key.ToString(),e); } }
        OpenCenter();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (_center?.IsLoaded != true) throw new InvalidOperationException("Central indisponível");
        _center.Close();
        OpenSettings();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (_settings?.IsLoaded != true) throw new InvalidOperationException("Configurações indisponíveis");
        if (capturePath != null) Capture(_settings, capturePath);
        _settings.Width = 390;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        _settings.VerifyPages();
        _settings.Close();
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
