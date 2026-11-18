using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private readonly DockPanel _layout = new();
    private readonly StackPanel _track = new();
    private readonly StackPanel _metric = new();
    private readonly StackPanel _remainingApps = new();
    private Popup? _quickPanel;
    private double _factor = 1;
    private int _thickness;
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
    public string? ClockText => _widgetTiles.TryGetValue("clock-card", out var card) || _widgetTiles.TryGetValue("clock",out card)
        ? SoBar.Widgets.WidgetService.ReadClock(card) : null;
    public bool HasClock => _widgetTiles.ContainsKey("clock") || _widgetTiles.ContainsKey("clock-card");
    public BarWindow(Shell shell, string monitorId)
    {
        Title = "SoBar";
        Style = (Style)FindResource(typeof(Window));
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Width = 720; Height = 56;
        Background = Brushes.Transparent;
        _shell = shell; MonitorId = monitorId;
        _launcher = Presentation.Button("", shell.OpenCenter, "Abrir a central SoBar");
        _launcher.Style = (Style)FindResource("DockButton");
        _launcher.Content = LauncherMark();
        AutomationProperties.SetName(_launcher, "Abrir a central SoBar");
        _settings = Presentation.Button("", ToggleQuickPanel, "Opções da barra");
        _settings.Style = (Style)FindResource("DockButton");
        _settings.Width = 25;
        _settings.FontSize = 13;
        _settings.BorderThickness = new Thickness(0);
        _settings.Background = Brushes.Transparent;
        _settings.FontFamily = new FontFamily("Segoe MDL2 Assets");
        AutomationProperties.SetName(_settings, "Opções da barra");
        _track.Children.Add(_apps);
        _track.Children.Add(_metric);
        _track.Children.Add(_remainingApps);
        _track.Children.Add(_widgets);
        _track.SizeChanged += (_, _) => UpdateDockSize();
        var menu = new ContextMenu();
        AddMenu(menu, "Abrir central", shell.OpenCenter);
        AddMenu(menu, "Configurações", shell.OpenSettings);
        AddMenu(menu, "Encerrar o SoBar", shell.Exit);
        _launcher.ContextMenu = menu;
        var inner = new Border { CornerRadius = new CornerRadius(13), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(36,225,231,244)), Padding = new Thickness(3), Child = _layout };
        var frame = new Border { CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(1), Padding = new Thickness(1), Child = inner };
        frame.SetResourceReference(Border.BackgroundProperty, "BarFill");
        frame.SetResourceReference(Border.BorderBrushProperty, "DockRim");
        Content = frame;
        SourceInitialized += (_, _) => _host = new DesktopBarHost(this, shell.Exit, shell.OpenCenter);
        Closing += (_, _) => { if (_quickPanel != null) _quickPanel.IsOpen = false; _host?.Dispose(); _host = null; };
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
        _track.Orientation = _apps.Orientation = _metric.Orientation = _remainingApps.Orientation = _widgets.Orientation = orientation;
        if (_quickPanel != null) _quickPanel.IsOpen = false;
        _settings.Content = s.Edge switch { "Top" => "\uE70D", "Left" => "\uE76C", "Right" => "\uE76B", _ => "\uE70E" };
        _settings.Height = _vertical ? 25 : 46;
        _settings.Width = _vertical ? 46 : 25;
        _layout.Children.Clear();
        DockPanel.SetDock(_launcher, _vertical ? Dock.Top : Dock.Left);
        DockPanel.SetDock(_settings, _vertical ? Dock.Bottom : Dock.Right);
        _layout.Children.Add(_launcher); _layout.Children.Add(_settings);
        _layout.Children.Add(Scroller(_track));
        _appSignature = null;
        var widgetKey = orientation + "/" + string.Join("\n", s.Widgets.Select(id => $"{id}:{s.WidgetScales.GetValueOrDefault(id,1d)}:{s.WidgetGroups.GetValueOrDefault(id)}")) + "/" + string.Join(",", plugins.Select(p => p.Id + ":" + p.Value));
        if (_widgetSignature != widgetKey)
        {
            _metric.Children.Clear();
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
                if ((id is "system-card" or "codex-limits") && group == null && id == s.Widgets[0]) _metric.Children.Add(tile);
                else container.Children.Add(tile);
            }
            foreach (var plugin in plugins)
            {
                var label = new TextBlock { Text = plugin.Value, ToolTip = plugin.Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5,0,5,0), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 140 };
                _widgets.Children.Add(label); _pluginLabels[plugin.Id] = label;
            }
            _widgetSignature = widgetKey;
        }
        Presentation.Glass(this, s);
        _factor = s.Size switch { "Compacto" => .85, "Grande" => 1.2, _ => 1d };
        _layout.LayoutTransform = new ScaleTransform(_factor, _factor);
        _thickness = 0;
        UpdateDockSize();
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
    private void UpdateDockSize()
    {
        if (_host == null) return;
        var extent = new Size(double.PositiveInfinity,double.PositiveInfinity);
        _track.Measure(extent); _launcher.Measure(extent); _settings.Measure(extent);
        var width = _vertical ? _track.DesiredSize.Height : _track.DesiredSize.Width;
        var ends = _vertical ? _launcher.DesiredSize.Height + _settings.DesiredSize.Height
                             : _launcher.DesiredSize.Width + _settings.DesiredSize.Width;
        var cross = _vertical ? _track.DesiredSize.Width : _track.DesiredSize.Height;
        // Insets include the two glass edges, their padding and rounding at fractional DPI.
        _host.SetPreferredLength(Math.Ceiling((width + ends) * _factor) + 14);
        var thickness = (int)Math.Ceiling(Math.Max(48,cross) * _factor) + 14;
        if (thickness == _thickness) return;
        _thickness = thickness;
        var s = _settingsSnapshot;
        _host.Configure(MonitorId,s.Edge,s.ReserveSpace,s.AutoHide,s.HideFullscreen,thickness);
    }
    private static FrameworkElement LauncherMark()
    {
        var grid = new UniformGrid { Columns = 2, Rows = 2, Width = 25, Height = 25 };
        foreach (var color in new[] { "#F8FAFD", "#CAD2DC", "#B0BCCA", "#8295AA" })
            grid.Children.Add(new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                CornerRadius = new CornerRadius(1), Margin = new Thickness(1) });
        return grid;
    }

    internal void ToggleQuickPanel()
    {
        if (_quickPanel?.IsOpen == true) { _quickPanel.IsOpen = false; return; }
        var panel = new StackPanel { MinWidth = 205 };
        var title = new TextBlock { Text = "SoBar", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(5,0,0,0) };
        var status = new TextBlock { Text = _settingsSnapshot.ReserveSpace ? "Área da barra reservada" : "Dock flutuante",
            FontSize = 12, Margin = new Thickness(5,0,0,9) };
        title.SetResourceReference(TextBlock.ForegroundProperty,"Ink");
        status.SetResourceReference(TextBlock.ForegroundProperty,"Muted");
        panel.Children.Add(title); panel.Children.Add(status);
        void AddCommand(string label, string glyph, Action action)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            var symbol = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center };
            symbol.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
            content.Children.Add(symbol); content.Children.Add(new TextBlock { Text = label });
            var button = Presentation.Button(label, () => { if (_quickPanel != null) _quickPanel.IsOpen = false; action(); });
            button.Content = content; button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(0,2,0,0); button.Padding = new Thickness(7,5,7,5);
            AutomationProperties.SetName(button,label);
            panel.Children.Add(button);
        }
        AddCommand("Pesquisar","\uE721",_shell.OpenCenter);
        AddCommand("Configurar barra","\uE713",_shell.OpenSettings);
        var border = new Border { Child = panel, Padding = new Thickness(10), Margin = new Thickness(3),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11) };
        border.Background = (Brush)FindResource("Base"); border.BorderBrush = (Brush)FindResource("DockRim");
        var placement = _settingsSnapshot.Edge switch { "Top" => PlacementMode.Bottom, "Left" => PlacementMode.Right,
            "Right" => PlacementMode.Left, _ => PlacementMode.Top };
        _quickPanel = new Popup { PlacementTarget = _settings, Placement = placement, Child = border, StaysOpen = false,
            AllowsTransparency = true, PopupAnimation = SystemParameters.ClientAreaAnimation ? PopupAnimation.Fade : PopupAnimation.None };
        foreach (var key in new[] { "Ink","Muted","Accent","Base","Surface","Line","Raised","DockEdge" })
            border.Resources[key] = FindResource(key);
        border.PreviewKeyDown += (_,e) => { if (e.Key == Key.Escape) { _quickPanel.IsOpen = false; _settings.Focus(); } };
        _quickPanel.Opened += (_,_) => { if (_settings.IsKeyboardFocused) panel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)); };
        _quickPanel.IsOpen = true;
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
        frame.SetResourceReference(Border.BorderBrushProperty, "DockRim");
        frame.SetResourceReference(Border.BackgroundProperty, "Surface");
        AutomationProperties.SetName(frame, "Grupo " + name);
        _widgets.Children.Add(frame);
        return group;
    }
    internal void OpenWidget(string id)
    {
        if (_widgetTiles.GetValueOrDefault(id) is not Button button) throw new InvalidOperationException("Widget ausente: " + id);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
        _remainingApps.Children.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            var panel = i < 3 ? _apps : _remainingApps;
            panel.Children.Add(AppButton(items[i]));
        }
        UpdateDockSize();
    }
    private Button AppButton(AppItem item)
    {
        var open = item.Windows.FirstOrDefault();
        var title = open?.Title ?? Path.GetFileNameWithoutExtension(item.Path);
        var active = item.Windows.Any(w => w.IsActive);
        var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var icon = open?.Icon ?? DesktopWindowService.IconForPath(item.Path);
        if (icon != null) stack.Children.Add(new Image { Source = icon, Width = 31, Height = 31 });
        else stack.Children.Add(new TextBlock { Text = "▣", FontSize = 18 });
        if (item.Windows.Count > 1) stack.Children.Add(new TextBlock { Text = item.Windows.Count.ToString(), Margin = new Thickness(3,0,0,0), FontSize = 11 });
        var grid = new Grid { Height = 40, MinWidth = 38 };
        grid.Children.Add(stack);
        if (open != null)
        {
            var indicator = new Rectangle { Width = active ? 6 : 3, Height = active ? 4 : 3, RadiusX = 2, RadiusY = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0,0,0,0) };
            indicator.SetResourceReference(Shape.FillProperty, active ? "Accent" : "Muted");
            grid.Children.Add(indicator);
        }
        var button = new Button { Style = (Style)FindResource("DockButton"), Content = grid, ToolTip = title };
        button.SetResourceReference(Control.BorderBrushProperty, active ? "Accent" : "DockEdge");
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
