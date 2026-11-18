using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SoBar.Core;
using SoBar.Windows;
using Path = System.IO.Path;

namespace SoBar.App;

internal sealed class BarWindow : Window
{
    private readonly Shell _shell;
    public string MonitorId { get; }
    private DesktopBarHost? _host;
    private readonly Grid _layout = new();
    private readonly StackPanel _apps = new();
    private readonly StackPanel _widgets = new();
    private readonly Button _launcher;
    private readonly Button _settings;
    public bool RequestedShortcutAvailable { get; private set; }
    public bool EmergencyAvailable => _host?.EmergencyHotkeyRegistered == true;
    private bool _vertical;
    private readonly Dictionary<string, FrameworkElement> _widgetTiles = new();
    internal string[] VisibleGroupNames => _widgets.Children.OfType<Border>().Select(group => (string)group.Tag).ToArray();
    private readonly Dictionary<string, TextBlock> _pluginLabels = new();
    private AppSettings _settingsSnapshot = new();
    private string? _widgetSignature;
    private string? _appSignature;
    public string? ClockText => (_widgetTiles.GetValueOrDefault("clock") is Button { Content: TextBlock clock }) ? clock.Text : null;
    public bool HasClock => _widgetTiles.ContainsKey("clock");
    public BarWindow(Shell shell, string monitorId)
    {
        Title = "SoBar";
        Style = (Style)FindResource(typeof(Window));
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Width = 720; Height = 56;
        Background = Brushes.Transparent;
        _shell = shell; MonitorId = monitorId;
        _launcher = Presentation.Button("SoBar", shell.OpenCenter, "Abrir a central");
        AutomationProperties.SetName(_launcher, "Abrir a central SoBar");
        _launcher.FontWeight = FontWeights.SemiBold;
        _launcher.Padding = new Thickness(8, 3, 10, 3);
        _settings = Presentation.Button("\uE713", shell.OpenSettings, "Configurações da barra");
        AutomationProperties.SetName(_settings, "Configurações da barra");
        _settings.FontFamily = new FontFamily("Segoe MDL2 Assets");
        _settings.Padding = new Thickness(7);
        var menu = new ContextMenu();
        AddMenu(menu, "Abrir central", shell.OpenCenter);
        AddMenu(menu, "Configurações", shell.OpenSettings);
        AddMenu(menu, "Encerrar o SoBar", shell.Exit);
        _launcher.ContextMenu = menu;
        var frame = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(5), Child = _layout };
        frame.SetResourceReference(Border.BackgroundProperty, "BarFill");
        frame.SetResourceReference(Border.BorderBrushProperty, "Line");
        Content = frame;
        SourceInitialized += (_, _) => _host = new DesktopBarHost(this, shell.Exit, shell.OpenCenter);
        Closing += (_, _) => { _host?.Dispose(); _host = null; };
        if (SystemParameters.ClientAreaAnimation)
            Loaded += (_, _) => frame.BeginAnimation(OpacityProperty, new DoubleAnimation(.35, 1, TimeSpan.FromMilliseconds(140)));
    }
    private static void AddMenu(ContextMenu menu, string caption, Action action)
    {
        var item = new MenuItem { Header = caption };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }
    public void Configure(AppSettings s, IReadOnlyList<PluginTile> plugins)
    {
        Presentation.Apply(s, Resources);
        _settingsSnapshot = s;
        _vertical = s.Edge is "Left" or "Right";
        var orientation = _vertical ? Orientation.Vertical : Orientation.Horizontal;
        _apps.Orientation = _widgets.Orientation = orientation;
        _layout.Children.Clear();
        _layout.ColumnDefinitions.Clear();
        _layout.RowDefinitions.Clear();
        var launcherContent = new StackPanel { Orientation = Orientation.Horizontal };
        var mark = new TextBlock { Text = "◒", FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 20, VerticalAlignment = VerticalAlignment.Center };
        mark.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        launcherContent.Children.Add(mark);
        if (!_vertical) launcherContent.Children.Add(new TextBlock { Text = "SoBar", Margin = new Thickness(6,0,0,0), VerticalAlignment = VerticalAlignment.Center });
        _launcher.Content = launcherContent;
        var appsView = Scroller(_apps);
        var widgetsView = Scroller(_widgets);
        var children = new FrameworkElement[] { _launcher, appsView, widgetsView, _settings };
        for (int i = 0; i < children.Length; i++)
        {
            var size = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
            if (_vertical) { _layout.RowDefinitions.Add(new RowDefinition { Height = size }); Grid.SetRow(children[i], i); }
            else { _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = size }); Grid.SetColumn(children[i], i); }
            _layout.Children.Add(children[i]);
        }
        void LimitWidgets()
        {
            if (_vertical) widgetsView.MaxHeight = Math.Max(50, ActualHeight * .55);
            else widgetsView.MaxWidth = Math.Max(50, ActualWidth * .55);
        }
        SizeChangedEventHandler onResize = (_, _) => LimitWidgets();
        SizeChanged += onResize;
        widgetsView.Unloaded += (_, _) => SizeChanged -= onResize;
        LimitWidgets();
        _appSignature = null;
        var widgetKey = orientation + "/" + string.Join("\n", s.Widgets.Select(id => $"{id}:{s.WidgetScales.GetValueOrDefault(id,1d)}:{s.WidgetGroups.GetValueOrDefault(id)}")) + "/" + string.Join(",", plugins.Select(p => p.Id + ":" + p.Value));
        if (_widgetSignature != widgetKey)
        {
            _widgets.Children.Clear();
            _pluginLabels.Clear();
            _widgetTiles.Clear();
            string? previousGroup = null;
            var container = _widgets;
            foreach (var id in s.Widgets)
            {
                if (!SoBar.Widgets.WidgetService.Catalog.ContainsKey(id)) continue;
                var group = s.WidgetGroups.GetValueOrDefault(id);
                if (group != previousGroup)
                {
                    container = group == null ? _widgets : AddGroup(group, orientation);
                    previousGroup = group;
                }
                var tile = _shell.Widgets.CreateTile(id);
                _widgetTiles[id] = tile;
                tile.Tag = id;
                var scale = s.WidgetScales.GetValueOrDefault(id,1d);
                tile.LayoutTransform = new ScaleTransform(scale,scale);
                tile.VerticalAlignment = VerticalAlignment.Center;
                container.Children.Add(tile);
            }
            foreach (var plugin in plugins)
            {
                var label = new TextBlock { Text = plugin.Value, ToolTip = plugin.Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5,0,5,0), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 140 };
                _widgets.Children.Add(label); _pluginLabels[plugin.Id] = label;
            }
            _widgetSignature = widgetKey;
        }
        Presentation.Glass(this, s);
        var factor = s.Size switch { "Compacto" => .85, "Grande" => 1.2, _ => 1d };
        _layout.LayoutTransform = new ScaleTransform(factor, factor);
        _host?.Configure(MonitorId, s.Edge, s.ReserveSpace, s.AutoHide, s.HideFullscreen, (int)Math.Round((_vertical ? 100 : 56) * factor));
        try
        {
            var gesture = Presentation.ParseShortcut(s.Shortcut);
            RequestedShortcutAvailable = _host?.SetCommandHotkey(gesture.Modifiers, gesture.Key) == true;
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException)
        {
            RequestedShortcutAvailable = false;
            _shell.Services.Log("Atalho indisponível; a central continua acessível pela barra.");
        }
    }
    private StackPanel AddGroup(string name, Orientation orientation)
    {
        var group = new StackPanel { Orientation = orientation };
        var label = new TextBlock {
            Text = name, FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(5,1,3,1),
            MaxWidth = 75, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        group.Children.Add(label);
        var frame = new Border { Child = group, Tag = name, ToolTip = name,
            CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1), Margin = new Thickness(2),
            VerticalAlignment = VerticalAlignment.Center };
        frame.SetResourceReference(Border.BorderBrushProperty, "Line");
        frame.SetResourceReference(Border.BackgroundProperty, "Surface");
        AutomationProperties.SetName(frame, "Grupo " + name);
        _widgets.Children.Add(frame);
        return group;
    }
    public void UpdatePlugins(IReadOnlyList<PluginTile> plugins)
    {
        foreach (var tile in plugins)
            if (_pluginLabels.TryGetValue(tile.Id, out var label)) label.Text = tile.Value;
    }
    private ScrollViewer Scroller(StackPanel panel)
    {
        if (panel.Parent is ScrollViewer previous) previous.Content = null;
        var viewer = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = _vertical ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = _vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden, VerticalContentAlignment = VerticalAlignment.Center };
        viewer.PreviewMouseWheel += (_, e) => {
            if (_vertical) viewer.ScrollToVerticalOffset(viewer.VerticalOffset - Math.Sign(e.Delta) * 50);
            else viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - Math.Sign(e.Delta) * 50);
            e.Handled = true;
        };
        return viewer;
    }
    private sealed record AppItem(string Path, List<DesktopWindow> Windows);
    public void UpdateApps(IReadOnlyList<DesktopWindow> windows)
    {
        var s = _settingsSnapshot;
        var items = s.PinnedApps.Select(p => new AppItem(p, [])).ToList();
        foreach (var w in windows)
        {
            if (s.AllMonitors && !string.Equals(w.MonitorId, MonitorId, StringComparison.OrdinalIgnoreCase)) continue;
            var matching = items.FirstOrDefault(i => (i.Windows.Count == 0 || s.GroupWindows) && i.Path.Length > 0 &&
                string.Equals(i.Path, w.ExecutablePath, StringComparison.OrdinalIgnoreCase));
            if (matching == null) items.Add(new AppItem(w.ExecutablePath, [w]));
            else matching.Windows.Add(w);
        }
        var signature = string.Join("|", items.Select(i => i.Path + ":" + string.Join(",", i.Windows.Select(w => $"{w.Handle}/{w.IsActive}/{w.Title}"))));
        if (signature == _appSignature) return;
        _appSignature = signature;
        _apps.Children.Clear();
        foreach (var item in items) _apps.Children.Add(AppButton(item));
    }
    private Button AppButton(AppItem item)
    {
        var open = item.Windows.FirstOrDefault();
        var title = open?.Title ?? Path.GetFileNameWithoutExtension(item.Path);
        var active = item.Windows.Any(w => w.IsActive);
        var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var icon = open?.Icon ?? DesktopWindowService.IconForPath(item.Path);
        if (icon != null) stack.Children.Add(new Image { Source = icon, Width = 22, Height = 22 });
        else stack.Children.Add(new TextBlock { Text = "▣", FontSize = 18 });
        if (item.Windows.Count > 1) stack.Children.Add(new TextBlock { Text = item.Windows.Count.ToString(), Margin = new Thickness(3,0,0,0), FontSize = 11 });
        var grid = new Grid();
        grid.Children.Add(stack);
        if (open != null)
        {
            var indicator = new Rectangle { Width = active ? 16 : 5, Height = 2, RadiusX = 1, RadiusY = 1, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0,0,0,-4) };
            indicator.SetResourceReference(Shape.FillProperty, active ? "Accent" : "Muted");
            grid.Children.Add(indicator);
        }
        var button = new Button { Content = grid, ToolTip = title, Padding = new Thickness(8,6,8,7), MinWidth = 34, HorizontalContentAlignment = HorizontalAlignment.Center };
        if (active) button.SetResourceReference(Control.BackgroundProperty, "Raised");
        AutomationProperties.SetName(button, title);
        var menu = new ContextMenu();
        foreach (var w in item.Windows)
        {
            AddMenu(menu, w.Title, () => _shell.Desktop.Activate(w.Handle));
            AddMenu(menu, "Pré-visualizar • " + w.Title, () => WindowPreview.Show(this, w.Handle));
        }
        if (File.Exists(item.Path))
        {
            var pinned = _settingsSnapshot.PinnedApps.Contains(item.Path, StringComparer.OrdinalIgnoreCase);
            AddMenu(menu, pinned ? "Desafixar da barra" : "Fixar na barra", () => _shell.TogglePin(item.Path));
        }
        button.ContextMenu = menu;
        button.Click += (_, _) => {
            if (item.Windows.Count > 1) { menu.PlacementTarget = button; menu.IsOpen = true; }
            else if (open != null) _shell.Desktop.Toggle(open.Handle);
            else _shell.Execute(() => _shell.Services.Launch(new SearchHit(title, item.Path, "Aplicativo")));
        };
        return button;
    }
}
