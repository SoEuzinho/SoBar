using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace SoBar.Widgets;

public sealed partial class WidgetService
{
    private void OpenPanel(string id, Window? owner)
    {
        if (disposed) return;
        if (panels.TryGetValue(id, out var existing)) { existing.Activate(); return; }
        var body = new StackPanel { Margin = new Thickness(22) };
        var panel = new Window
        {
            Title = $"SoBar · {Catalog[id]}", Width = 374, SizeToContent = SizeToContent.Height,
            MaxHeight = Math.Min(680, SystemParameters.WorkArea.Height), WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.CanResizeWithGrip, ShowInTaskbar = false, Topmost = true,
            Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        if (owner is { IsLoaded: true }) panel.Owner = owner;
        body.Children.Add(new TextBlock { Text = Catalog[id], FontSize = 22, FontWeight = FontWeights.SemiBold });
        var valueText = new TextBlock { FontSize = 28, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
        var detail = new TextBlock { FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 14) };
        if (id != "notes") { body.Children.Add(valueText); body.Children.Add(detail); }
        panels[id] = panel;
        updatePanels[id] = reading => { valueText.Text = reading.Short; detail.Text = reading.Detail; };
        switch (id)
        {
            case "calendar" or "clock":
                body.Children.Add(new Calendar { SelectedDate = DateTime.Today }); break;
            case "notes": BuildNotes(body, panel); break;
            case "weather": BuildWeather(body); break;
            case "ai-usage": BuildOrganizationUsage(body); break;
            case "stopwatch":
                AddAction(body, "Iniciar / pausar", () =>
                {
                    if (stopwatch.IsRunning) stopwatch.Stop(); else stopwatch.Start();
                    Refresh(id);
                });
                AddAction(body, "Zerar", () => { stopwatch.Reset(); Refresh(id); });
                break;
            case "pomodoro":
                body.Children.Add(Paragraph("25 minutos de foco e 5 de pausa. Ao terminar, inicie a próxima etapa."));
                AddAction(body, "Iniciar / pausar / próxima etapa", () => { focus.Toggle(); Refresh(id); });
                AddAction(body, "Reiniciar etapa", () => { focus.Reset(); Refresh(id); });
                break;
            case "temperature" or "gpu":
                body.Children.Add(Paragraph("GPU e temperaturas são lidas pelo LibreHardwareMonitor quando o hardware disponibiliza sensores."));
                AddAction(body, "Ativar sensores nesta sessão", () =>
                {
                    sensors ??= new();
                    lastRead.Remove("sensors");
                    Refresh();
                });
                AddAction(body, "Desativar sensores", () =>
                {
                    var previous = sensors;
                    sensors = null;
                    if (previous != null) _ = Task.Run(previous.Dispose);
                    Refresh();
                });
                break;
            case "volume" or "microphone": BuildAudio(body, id); break;
            case "music":
                AddAction(body, "Faixa anterior", () => Audio.MediaCommand(new WindowInteropHelper(panel).Handle, 12));
                AddAction(body, "Reproduzir / pausar", () => Audio.MediaCommand(new WindowInteropHelper(panel).Handle, 14));
                AddAction(body, "Próxima faixa", () => Audio.MediaCommand(new WindowInteropHelper(panel).Handle, 11)); break;
        }
        panel.Closed += (_, _) =>
        {
            panels.Remove(id);
            updatePanels.Remove(id);
            if (tiles.Count == 0 && panels.Count == 0) timer.Stop();
        };
        panel.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) panel.Close(); };
        if (cache.TryGetValue(id, out var known)) updatePanels[id](known);
        Refresh(id);
        panel.Show();
        timer.Start();
    }

    private static TextBlock Paragraph(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 8) };

    private static Button AddAction(Panel host, string label, Action action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(10, 7, 10, 7) };
        button.Click += (_, _) => action();
        host.Children.Add(button);
        return button;
    }

    private void BuildAudio(StackPanel body, string id)
    {
        var message = Paragraph("");
        bool capture = id == "microphone";
        // Read the default endpoint again on each action in case USB devices have changed.
        void Set(float? delta = null)
        {
            try
            {
                var current = Audio.Read(capture);
                Audio.Change(capture, delta.HasValue ? current.Volume + delta.Value : null, delta.HasValue ? null : !current.Muted);
                Publish(id, Audio.Reading(capture));
                message.Text = "";
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { message.Text = $"Dispositivo indisponível · {ex.GetBaseException().Message}"; }
        }
        AddAction(body, "Aumentar 5%", () => Set(.05f));
        AddAction(body, "Diminuir 5%", () => Set(-.05f));
        AddAction(body, "Ativar / desativar mudo", () => Set());
        body.Children.Add(message);
    }

    private void BuildOrganizationUsage(StackPanel body)
    {
        body.Children.Add(Paragraph("Tokens de entrada e saída de Completions da organização desde 00:00 UTC. A API informa consumo. Os limites da assinatura do ChatGPT pertencem a outro serviço."));
        body.Children.Add(Paragraph("Ao conectar, você permite uma consulta a api.openai.com a cada 15 minutos enquanto o widget estiver em uso. A chave Admin fica protegida pela conta do Windows."));
        var secret = new PasswordBox { MaxLength = 4096, Padding = new Thickness(6) };
        ThemeField(secret);
        AddFieldLabel(body, "Chave Admin da OpenAI", secret);
        body.Children.Add(secret);
        var status = Paragraph(usage.Connected ? "Credencial salva neste computador." : "");
        void Change(bool connect)
        {
            try
            {
                if (connect) usage.Connect(secret.Password.Trim()); else usage.Disconnect();
                usageRevision++;
                secret.Clear();
                status.Text = connect ? "Credencial salva. Consultando o uso…" : "Credencial removida.";
                lastRead.Remove("ai-usage");
                if (connect) Publish("ai-usage", new("…", "Consultando o uso da organização…"));
                Refresh("ai-usage");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { status.Text = "Falha ao salvar ou remover a chave. Verifique o valor e o acesso ao diretório do aplicativo."; }
        }
        AddAction(body, "Salvar chave e autorizar consultas", () => Change(true));
        AddAction(body, "Desconectar e apagar chave", () => Change(false));
        body.Children.Add(status);
    }

    private void BuildWeather(StackPanel body)
    {
        body.Children.Add(Paragraph("A consulta envia as coordenadas informadas ao Open-Meteo. A atualização ocorre a cada 15 minutos enquanto o widget estiver visível."));
        var latitude = Field(body, "Latitude", coordinates?.Latitude.ToString("F4", Portuguese));
        var longitude = Field(body, "Longitude", coordinates?.Longitude.ToString("F4", Portuguese));
        var status = Paragraph("");
        AddAction(body, "Consultar Open-Meteo", () =>
        {
            if (!Weather.TryCoordinates(latitude.Text, longitude.Text, out var lat, out var lon))
            {
                status.Text = "Use uma latitude entre −90 e 90 e uma longitude entre −180 e 180.";
                return;
            }
            status.Text = "Consulta ativada nesta sessão.";
            coordinates = (lat, lon);
            weatherRevision++;
            lastRead.Remove("weather");
            Publish("weather", new("…", "Consultando o Open-Meteo…"));
            Refresh("weather");
        });
        AddAction(body, "Desativar consultas", () => { coordinates = null; weatherRevision++; status.Text = ""; Refresh("weather"); });
        body.Children.Add(status);
        body.Children.Add(Paragraph("Dados meteorológicos · Open-Meteo (CC BY 4.0)"));
    }

    private static TextBox Field(StackPanel body, string label, string? initial)
    {
        var box = new TextBox { Text = initial ?? "", Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 6) };
        AddFieldLabel(body, label, box);
        body.Children.Add(box);
        return box;
    }

    private static void AddFieldLabel(Panel body, string label, Control field)
    {
        var caption = new Label { Content = label, Target = field, Padding = new Thickness(0, 3, 0, 3) };
        if (Application.Current?.TryFindResource("Ink") is Brush)
            caption.SetResourceReference(Control.ForegroundProperty, "Ink");
        body.Children.Add(caption);
    }

    private static void ThemeField(Control field)
    {
        if (Application.Current?.TryFindResource("Surface") is not Brush) return;
        field.SetResourceReference(Control.BackgroundProperty, "Surface");
        field.SetResourceReference(Control.ForegroundProperty, "Ink");
        field.SetResourceReference(Control.BorderBrushProperty, "Line");
    }

    private void BuildNotes(StackPanel body, Window panel)
    {
        var path = Path.Combine(directory, "notes.txt");
        var editor = new TextBox
        {
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 160, MaxHeight = 320, Margin = new Thickness(0, 10, 0, 5), Padding = new Thickness(9), MaxLength = 65536
        };
        var status = Paragraph("");
        body.Children.Add(editor);
        body.Children.Add(status);
        try { editor.Text = File.ReadAllText(path); }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status.Text = $"Falha ao abrir as notas · {ex.Message}";
            editor.IsReadOnly = true;
            return;
        }
        bool dirty = false;
        editor.TextChanged += (_, _) => { dirty = true; status.Text = "Alterações a salvar"; };
        bool Save()
        {
            if (!dirty) return true;
            string? temporary = null;
            try
            {
                Directory.CreateDirectory(directory);
                temporary = Path.Combine(directory, $".notes-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(temporary, editor.Text);
                File.Move(temporary, path, true);
                dirty = false;
                status.Text = "Salvo neste computador";
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = $"Falha ao salvar · {ex.Message}"; return false; }
            finally { if (temporary != null) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
        }
        AddAction(body, "Salvar notas", () => Save());
        panel.Closing += (_, e) => { if (!Save() && !disposed) e.Cancel = MessageBox.Show(panel, "Fechar e descartar as alterações?", "Notas", MessageBoxButton.YesNo) != MessageBoxResult.Yes; };
    }
}
