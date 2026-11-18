using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SoBar.Core;
using SoBar.Windows;
using SoBar.Widgets;

namespace SoBar.App;

public partial class SettingsWindow : Window
{
    private readonly Shell _shell;
    private readonly Dictionary<string,Button> _nav = [];
    private string _section = "Aparência";
    private AppSettings S => _shell.Services.Settings;
    internal SettingsWindow(Shell shell)
    {
        _shell = shell;
        InitializeComponent();
        Status.Text = shell.ShortcutStatus;
        var glyphs = new[] { "\uE771", "\uE7F4", "\uECA5", "\uE8F1", "\uE774" };
        foreach (var name in new[] { "Aparência", "Barra", "Widgets", "Perfis", "Conexões" })
        {
            var tab = Presentation.Button(name, () => ShowPage(name));
            var navLabel = new StackPanel { Orientation = Orientation.Horizontal };
            var glyph = new TextBlock { Text = glyphs[_nav.Count], FontSize = 14, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Segoe MDL2 Assets"), Margin = new Thickness(0,0,8,0) };
            glyph.SetResourceReference(ForegroundProperty, "Muted");
            navLabel.Children.Add(glyph); navLabel.Children.Add(new TextBlock { Text=name }); tab.Content=navLabel;
            AutomationProperties.SetName(tab,name);
            tab.Margin = new Thickness(0,1,2,2);
            tab.HorizontalContentAlignment = HorizontalAlignment.Left;
            _nav[name] = tab; Nav.Children.Add(tab);
        }
        SizeChanged += (_, _) => { NavColumn.Width = new GridLength(ActualWidth < 560 ? 131 : 157); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        ShowPage("Aparência");
    }
    internal void VerifyPages()
    {
        foreach (var name in _nav.Keys) { ShowPage(name); UpdateLayout(); if (Page.ActualWidth <= 0) throw new InvalidOperationException(name); }
    }
    private void ExitClick(object sender, RoutedEventArgs e) => _shell.Exit();
    private void ShowPage(string name)
    {
        _section = name;
        foreach (var (key, control) in _nav)
        {
            control.SetResourceReference(Control.BackgroundProperty, key == name ? "Raised" : "Surface");
            control.SetResourceReference(Control.BorderBrushProperty, key == name ? "Line" : "Surface");
        }
        Page.Children.Clear(); Scroll.ScrollToTop();
        Page.Children.Add(new TextBlock { Text = name, FontSize = 26, FontWeight = FontWeights.SemiBold });
        switch (name)
        {
            case "Aparência": Appearance(); break;
            case "Barra": Behavior(); break;
            case "Widgets": Widgets(); break;
            case "Perfis": Profiles(); break;
            case "Conexões": Connections(); break;
        }
    }
    private void SectionHeading(string text) => Page.Children.Add(new TextBlock { Text = text, Style = (Style)FindResource("Section") });
    private void Hint(string text) => Page.Children.Add(new TextBlock { Text = text, Style = (Style)FindResource("Hint") });
    private WrapPanel Row()
    {
        var row = new WrapPanel { Margin = new Thickness(0,2,0,3) }; Page.Children.Add(row); return row;
    }
    private static void Add(WrapPanel row, string text, Action onClick) => row.Children.Add(Presentation.Button(text, onClick));
    private static TextBox Input(WrapPanel row, string value, string name, double width = 150)
    {
        var box = new TextBox { Text = value, Width = width, Margin = new Thickness(0,0,5,2), ToolTip = name };
        AutomationProperties.SetName(box, name); row.Children.Add(box); return box;
    }
    private void Check(string text, bool current, Action<AppSettings,bool> write)
    {
        var box = new CheckBox { Content = text, IsChecked = current };
        box.Click += (_, _) => { if (!Change(s => write(s, box.IsChecked == true))) box.IsChecked = !box.IsChecked; };
        Page.Children.Add(box);
    }
    private void Choices(IEnumerable<(string Key,string Label)> choices, string selected, Action<AppSettings,string> apply)
    {
        var row = Row();
        foreach (var choice in choices)
        {
            var control = Presentation.Button(choice.Label, () => {
                if (Change(s => apply(s, choice.Key))) ShowPage(_section);
            });
            if (choice.Key == selected) control.SetResourceReference(Control.BorderBrushProperty, "Accent");
            row.Children.Add(control);
        }
    }
    private bool Change(Action<AppSettings> mutate)
    {
        return _shell.Execute(() => {
            var draft = S;
            mutate(draft);
            draft.Validate();
            Presentation.ParseShortcut(draft.Shortcut);
            _shell.Services.Save(draft); _shell.Reconfigure();
            Status.Text = "Salvo   ·   " + _shell.ShortcutStatus;
        });
    }
    private void Appearance()
    {
        SectionHeading("Tema");
        var themes = Row();
        foreach (var (id, label, baseColor, endColor) in new[] {
            ("Escuro","Black","#14201A","#283A2F"), ("Claro","White","#F4F7F3","#C1D2CA"),
            ("Midnight","Midnight","#141B31","#3D4C81"), ("Liquid Glass","Liquid Glass","#193735","#638A7C"),
            ("iOS Glass","iOS Glass","#CBDCF2","#9ABCB9") })
        {
            var chip = Presentation.Button(label, () => { if (Change(s => s.Theme = id)) ShowPage(_section); });
            chip.Padding = new Thickness(5);
            chip.Margin = new Thickness(0,0,5,5);
            var sample = new StackPanel { Width = 79 };
            var preview = new Grid { Height = 34 };
            var colorA = (Color)ColorConverter.ConvertFromString(baseColor);
            var colorB = (Color)ColorConverter.ConvertFromString(endColor);
            preview.Children.Add(new Border { Background = new LinearGradientBrush(colorA,colorB,22), CornerRadius = new CornerRadius(4) });
            var strip = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center };
            foreach (var width in new[] { 8d, 17d, 8d })
                strip.Children.Add(new Border { Width=width, Height=5, CornerRadius=new CornerRadius(3), Background=id is "Claro" or "iOS Glass" ? Brushes.DarkSlateGray : Brushes.WhiteSmoke, Opacity=.78, Margin=new Thickness(1.5,0,1.5,0) });
            preview.Children.Add(strip); sample.Children.Add(preview);
            sample.Children.Add(new TextBlock { Text = label, FontSize = 12, Margin = new Thickness(2,5,2,1) });
            chip.Content = sample; AutomationProperties.SetName(chip, label);
            if (id == S.Theme) chip.SetResourceReference(BorderBrushProperty,"Accent");
            themes.Children.Add(chip);
        }
        SectionHeading("Tamanho");
        Choices([("Compacto","Compacto"),("Normal","Normal"),("Grande","Grande")], S.Size, (s,x) => s.Size = x);
        SectionHeading("Cor de destaque");
        var colors = Row();
        foreach (var color in new[] { "#57D9AF", "#8DBBFF", "#EFA372", "#CA9AF6", "#F08DA7" })
        {
            var chip = Presentation.Button("●", () => { if (Change(s => s.Accent = color)) ShowPage(_section); }, color);
            chip.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); chip.FontSize = 19;
            AutomationProperties.SetName(chip, "Usar cor " + color);
            colors.Children.Add(chip);
        }
        var custom = Row();
        var hex = Input(custom, S.Accent, "Cor hexadecimal", 110);
        Add(custom, "Aplicar cor", () => Change(s => s.Accent = hex.Text.Trim()));
        SectionHeading("Opacidade do fundo");
        var slider = new Slider { Value = Math.Round(S.Opacity * 100), Minimum = 20, Maximum = 100, TickFrequency = 5, IsSnapToTickEnabled = true, SmallChange = 5, LargeChange = 10, AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft };
        AutomationProperties.SetName(slider, "Opacidade em porcentagem");
        var fraction = new TextBlock { Text = $"{slider.Value:F0}%" };
        var debounce = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        void Commit()
        {
            debounce.Stop();
            if (Math.Abs(S.Opacity * 100 - slider.Value) > .01) Change(s => s.Opacity = slider.Value / 100);
        }
        debounce.Tick += (_, _) => Commit();
        slider.ValueChanged += (_, _) => { fraction.Text = $"{slider.Value:F0}%"; debounce.Stop(); debounce.Start(); };
        slider.Unloaded += (_, _) => { if (debounce.IsEnabled) Commit(); };
        Page.Children.Add(slider); Page.Children.Add(fraction);
    }
    private void Behavior()
    {
        SectionHeading("Posição");
        Choices([("Top","Superior"),("Bottom","Inferior"),("Left","Esquerda"),("Right","Direita")], S.Edge, (s,x) => s.Edge = x);
        SectionHeading("Monitores");
        Choices(new[] { ("","Principal") }.Concat(DesktopBarHost.Monitors().Select(m => (m.Id,m.Name))), S.MonitorId, (s,x) => s.MonitorId = x);
        Check("Mostrar uma barra por monitor", S.AllMonitors, (s,x) => s.AllMonitors = x);
        SectionHeading("Comportamento");
        Check("Reservar espaço na área de trabalho", S.ReserveSpace, (s,x) => s.ReserveSpace = x);
        Check("Ocultar automaticamente", S.AutoHide, (s,x) => s.AutoHide = x);
        Check("Ocultar sobre aplicativos em tela cheia", S.HideFullscreen, (s,x) => s.HideFullscreen = x);
        Check("Agrupar janelas do mesmo aplicativo", S.GroupWindows, (s,x) => s.GroupWindows = x);
        Check("Iniciar com o Windows", S.StartWithWindows, (s,x) => s.StartWithWindows = x);
        SectionHeading("Atalho da central");
        var shortcut = Row(); var key = Input(shortcut, S.Shortcut, "Combinação de teclas");
        Add(shortcut,"Aplicar",() => Change(s => s.Shortcut = key.Text.Trim()));
        Hint("Clique com o botão direito em um aplicativo aberto para fixar ou pré-visualizar a janela.");
        SectionHeading("Aplicativos fixados");
        foreach (var path in S.PinnedApps.ToArray())
        {
            var row = Row(); Add(row,"Desafixar • " + Path.GetFileNameWithoutExtension(path),() => { Change(s => s.PinnedApps.Remove(path)); ShowPage(_section); });
        }
        Add(Row(),"Fixar aplicativo…",() => {
            var file = new OpenFileDialog { Filter = "Aplicativo (*.exe)|*.exe" };
            if (file.ShowDialog(this) == true && Change(s => s.PinnedApps.Add(file.FileName))) ShowPage(_section);
        });
    }
    private void Widgets()
    {
        Hint("Arraste os widgets ou use as setas para ordenar. Widgets vizinhos com o mesmo nome de grupo ficam no mesmo bloco.");
        SectionHeading("Layout da barra");
        foreach (var id in S.Widgets.Where(WidgetService.Catalog.ContainsKey))
        {
            var line = Row(); line.AllowDrop = true;
            var handle = new TextBlock { Text = "⠿  " + WidgetService.Catalog[id], MinWidth = 120, Margin = new Thickness(0,0,5,0), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.SizeAll };
            Point start = default;
            handle.PreviewMouseLeftButtonDown += (_,e) => start = e.GetPosition(handle);
            handle.MouseMove += (_,e) => {
                if (e.LeftButton != MouseButtonState.Pressed) return;
                var delta = e.GetPosition(handle) - start;
                if (Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance && Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance) return;
                DragDrop.DoDragDrop(handle,new DataObject("SoBar.WidgetId",id),DragDropEffects.Move);
            };
            line.Drop += (_,e) => { if (e.Data.GetData("SoBar.WidgetId") is string source) Move(source,S.Widgets.IndexOf(id)); };
            line.Children.Add(handle);
            Add(line,"↑",() => Move(id,S.Widgets.IndexOf(id)-1)); Add(line,"↓",() => Move(id,S.Widgets.IndexOf(id)+1));
            var factors = new[] { .75, 1d, 1.25, 1.5 };
            var scale = new ComboBox { ItemsSource = factors.Select(x => $"{x * 100:F0}%"), SelectedIndex = Array.IndexOf(factors,S.WidgetScales.GetValueOrDefault(id,1d)), VerticalAlignment = VerticalAlignment.Center, MinWidth = 58, ToolTip = "Escala de " + WidgetService.Catalog[id] };
            scale.SelectionChanged += (_,_) => { if (scale.SelectedIndex >= 0) Change(s => s.WidgetScales[id] = factors[scale.SelectedIndex]); };
            line.Children.Add(scale);
            var group = Input(line, S.WidgetGroups.GetValueOrDefault(id) ?? "", "Grupo de " + WidgetService.Catalog[id], 100);
            group.MaxLength = 32;
            Add(line,"Definir grupo",() => {
                if (Change(s => {
                    var name = group.Text.Trim();
                    if (name.Length == 0) s.WidgetGroups.Remove(id); else s.WidgetGroups[id] = name;
                })) ShowPage(_section);
            });
            Add(line,"Remover",() => { if (Change(s => s.Widgets.Remove(id))) ShowPage(_section); });
        }
        var groups = S.WidgetGroups.Values.Distinct().OrderBy(x => x).ToArray();
        if (groups.Length > 0) SectionHeading("Nomes dos grupos");
        foreach (var name in groups)
        {
            var row = Row();
            var box = Input(row,name,"Renomear grupo " + name);
            box.MaxLength = 32;
            void Rename(string target)
            {
                if (Change(s => {
                    foreach (var entry in s.WidgetGroups.Where(p => p.Value == name).ToArray())
                        if (target.Length == 0) s.WidgetGroups.Remove(entry.Key); else s.WidgetGroups[entry.Key] = target;
                })) ShowPage(_section);
            }
            Add(row,"Renomear",() => Rename(box.Text.Trim()));
            Add(row,"Desagrupar",() => Rename(""));
        }
        SectionHeading("Adicionar à barra");
        var available = Row();
        foreach (var item in WidgetService.Catalog.Where(x => !S.Widgets.Contains(x.Key)))
            Add(available, "+ " + item.Value, () => { if (Change(s => s.Widgets.Add(item.Key))) ShowPage(_section); });
    }
    private void Move(string id, int target)
    {
        if (!S.Widgets.Contains(id) || target < 0 || target >= S.Widgets.Count || S.Widgets.IndexOf(id) == target) return;
        if (Change(s => { s.Widgets.Remove(id); s.Widgets.Insert(target,id); })) ShowPage(_section);
    }
    private void Profiles()
    {
        SectionHeading("Perfis de configuração");
        var names = Row();
        foreach (var name in _shell.Services.Profiles())
            Add(names,name,() => _shell.Execute(() => { _shell.Services.LoadProfile(name); _shell.Reconfigure(); ShowPage(_section); Status.Text = "Perfil carregado • " + name; }));
        var save = Row(); var input = Input(save,"","Nome do perfil");
        Add(save,"Salvar perfil",() => _shell.Execute(() => { _shell.Services.SaveProfile(input.Text.Trim()); _shell.Reconfigure(); ShowPage(_section); Status.Text = "Perfil salvo."; }));
        ContextRules();
        SectionHeading("Transferir configurações");
        var transfer = Row();
        Add(transfer,"Exportar…",() => {
            var dialog = new SaveFileDialog { Filter = "Configurações JSON|*.json", FileName = "SoBar.json" };
            if (dialog.ShowDialog(this) == true) _shell.Execute(() => { _shell.Services.Export(dialog.FileName); Status.Text = "Configurações exportadas."; });
        });
        Add(transfer,"Importar…",() => {
            var dialog = new OpenFileDialog { Filter = "Configurações JSON|*.json" };
            if (dialog.ShowDialog(this) == true && Confirm("Aplicar as configurações do arquivo escolhido?"))
                _shell.Execute(() => { _shell.Services.Import(dialog.FileName); _shell.Reconfigure(); ShowPage(_section); Status.Text = "Configurações importadas."; });
        });
    }
    private bool Confirm(string message) => MessageBox.Show(this,message,"SoBar",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No) == MessageBoxResult.Yes;
    private void Connections()
    {
        SectionHeading("Assistente OpenAI");
        Hint(_shell.Services.HasApiKey ? "Uma chave está salva neste usuário do Windows." : "Insira sua chave da API para usar o assistente.");
        var row = Row(); var password = new PasswordBox { Width = 170, Padding = new Thickness(7), Margin = new Thickness(0,0,5,0) };
        AutomationProperties.SetName(password,"Chave da API OpenAI"); row.Children.Add(password);
        Add(row,"Salvar chave",() => _shell.Execute(() => { if (password.Password.Length == 0) throw new InvalidDataException("Insira uma chave."); _shell.Services.SetApiKey(password.Password); password.Clear(); ShowPage(_section); }));
        if (_shell.Services.HasApiKey) Add(row,"Remover chave",() => _shell.Execute(() => { _shell.Services.SetApiKey(""); ShowPage(_section); }));
        Check("Guardar histórico de buscas locais",S.HistoryEnabled,(s,x) => s.HistoryEnabled=x);
        SectionHeading("Extensões instaladas");
        foreach (var plugin in _shell.Services.PluginTiles())
            Add(Row(),"Remover • " + plugin.Name,() => _shell.Execute(() => { _shell.Services.RemovePlugin(plugin.Id); _shell.Reconfigure(); ShowPage(_section); }));
        Add(Row(),"Importar manifesto…",() => {
            var file = new OpenFileDialog { Filter = "Manifesto JSON|*.json" };
            if (file.ShowDialog(this) != true) return;
            _shell.Execute(() => {
                var preview = _shell.Services.PreviewPlugin(file.FileName);
                if (!Confirm("Autorizar a extensão?\n\n" + preview)) return;
                _shell.Services.ImportPlugin(file.FileName); _shell.Reconfigure(); ShowPage(_section); Status.Text="Extensão importada.";
            });
        });
        SectionHeading("Atualização");
        Hint("Selecione o instalador MSI da nova versão. A assinatura .msi.sig deve estar na mesma pasta.");
        Add(Row(),"Selecionar atualização…",() => {
            var file = new OpenFileDialog { Filter = "Instalador do SoBar|*.msi" };
            if (file.ShowDialog(this) == true && Confirm("Verificar a assinatura, iniciar a atualização e fechar o SoBar?"))
                _shell.Execute(() => { _shell.Services.InstallUpdate(file.FileName); _shell.Exit(); });
        });
    }
}
