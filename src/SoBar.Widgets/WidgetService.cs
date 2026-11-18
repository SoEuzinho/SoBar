using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SoBar.Widgets;

public sealed partial class WidgetService : IDisposable
{
    partial void RefreshCodexLimits();
    partial void BuildCodexPanel(StackPanel body);
    partial void DisposeCodexLimits();
    public static IReadOnlyDictionary<string, string> Catalog { get; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
    {
        ["clock"] = "Relógio", ["calendar"] = "Calendário", ["weather"] = "Clima",
        ["cpu"] = "CPU", ["gpu"] = "GPU", ["ram"] = "RAM", ["disk"] = "Disco", ["network"] = "Rede",
        ["temperature"] = "Temperatura", ["music"] = "Música", ["volume"] = "Volume", ["microphone"] = "Microfone",
        ["battery"] = "Bateria", ["notes"] = "Notas", ["stopwatch"] = "Cronômetro", ["pomodoro"] = "Pomodoro",
        ["ai-usage"] = "Uso da API OpenAI", ["system-card"] = "CPU e memória", ["agenda"] = "Agenda local",
        ["power-card"] = "Energia", ["clock-card"] = "Data, hora e clima", ["codex-limits"] = "Limites do Codex",
        ["screen-time"] = "Tempo de tela"
    });

    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");
    private readonly string directory;
    private readonly Agenda agenda;
    private readonly ScreenTimeStore screenTime;
    private readonly DispatcherTimer timer;
    private readonly Dictionary<string, HashSet<Button>> tiles = [];
    private readonly Dictionary<string, Window> panels = [];
    private readonly Dictionary<string, Action<Reading>> updatePanels = [];
    private readonly Dictionary<string, Reading> cache = [];
    private readonly Dictionary<string, long> lastRead = [];
    private readonly HashSet<string> sampling = [];
    private readonly SystemReadings metrics = new();
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(8) };
    private readonly OrganizationUsage usage;
    private long usageRevision;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Stopwatch stopwatch = new();
    private readonly FocusTimer focus = new(TimeProvider.System);
    private Sensors? sensors;
    private (double Latitude, double Longitude)? coordinates;
    private long weatherRevision;
    private bool disposed;

    public WidgetService(string dataDirectory)
    {
        directory = dataDirectory;
        agenda = new(dataDirectory);
        screenTime = new(dataDirectory);
        usage = new(dataDirectory);
        timer = new(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher.CurrentDispatcher);
        timer.Stop();
    }

    public FrameworkElement CreateTile(string id)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!Catalog.ContainsKey(id)) throw new ArgumentOutOfRangeException(nameof(id));
        var caption = new TextBlock
        {
            Text = Catalog[id], TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 120, VerticalAlignment = VerticalAlignment.Center
        };
        var button = new Button
        {
            Content = caption, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 3, 8, 3), Cursor = System.Windows.Input.Cursors.Hand,
            FontSize = 12, FontFamily = new FontFamily("Segoe UI"), ToolTip = Catalog[id]
        };
        if (DockCard.Supports(id))
        {
            button.Content = new DockCard(id);
            button.Height = 46;
            button.MaxWidth = 168;
            button.Margin = new Thickness(2, 1, 2, 1);
            button.Padding = new Thickness(0);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
        }
        if (Application.Current?.TryFindResource("Ink") is Brush)
            button.SetResourceReference(Control.ForegroundProperty, "Ink");
        AutomationProperties.SetName(button, Catalog[id]);
        button.Click += (_, _) => OpenPanel(id, button);
        button.Loaded += (_, _) =>
        {
            if (disposed) return;
            if (!tiles.TryGetValue(id, out var group)) tiles[id] = group = [];
            group.Add(button);
            if (cache.TryGetValue(id, out var known)) Display(id, button, known);
            Refresh();
            timer.Start();
        };
        button.Unloaded += (_, _) =>
        {
            if (tiles.TryGetValue(id, out var group) && group.Remove(button) && group.Count == 0)
            {
                tiles.Remove(id);
                if (id == "screen-time") PauseScreenTime();
            }
            if (tiles.Count == 0 && panels.Count == 0) timer.Stop();
        };
        return button;
    }

    private static void Display(string id, Button button, Reading value)
    {
        if (button.Content is DockCard card)
        {
            card.Set(value);
            button.ToolTip = value.Detail;
            AutomationProperties.SetItemStatus(button, value.Short);
            AutomationProperties.SetHelpText(button, value.Detail);
            return;
        }
        var label = id switch { "cpu" => "CPU ", "gpu" => "GPU ", "ram" => "RAM ", "disk" => "Disco ", "volume" => "Vol ", "microphone" => "Mic ", _ => "" };
        var block = (TextBlock)button.Content;
        var text = label + value.Short;
        if (block.Text != text) block.Text = text;
        if (!Equals(button.ToolTip, value.Detail)) button.ToolTip = value.Detail;
        AutomationProperties.SetHelpText(button, value.Detail);
    }

    public static string? ReadClock(FrameworkElement tile) => tile is Button button
        ? button.Content switch { DockCard card => card.ClockTime, TextBlock text when AutomationProperties.GetName(button) == Catalog["clock"] => text.Text, _ => null } : null;

    private void Publish(string id, Reading reading)
    {
        if (disposed) return;
        if (cache.TryGetValue(id, out var previous) && previous == reading) return;
        cache[id] = reading;
        if (tiles.TryGetValue(id, out var group))
            foreach (var button in group) Display(id, button, reading);
        if (updatePanels.TryGetValue(id, out var update)) update(reading);
    }

    private void Refresh()
    {
        if (disposed) return;
        foreach (var id in tiles.Keys.Concat(panels.Keys).Distinct().ToArray()) Refresh(id);
    }

    private void Refresh(string id)
    {
        var now = DateTime.Now;
        switch (id)
        {
            case "screen-time": RefreshScreenTime(); return;
            case "codex-limits": RefreshCodexLimits(); return;
            case "system-card":
                Refresh("cpu");
                Refresh("ram");
                var cpu = cache.GetValueOrDefault("cpu");
                var ram = cache.GetValueOrDefault("ram");
                Publish(id, new($"CPU {cpu?.Short ?? "—"}  ·  RAM {ram?.Short ?? "—"}",
                    $"{cpu?.Detail ?? "Aguardando dados da CPU."}\n{ram?.Detail ?? "Aguardando dados da RAM."}",
                    Card: new(cpu?.Short ?? "—", ram?.Short ?? "—", Upper: cpu?.Percent, Lower: ram?.Percent)));
                return;
            case "clock-card":
                if (coordinates != null) Refresh("weather");
                var forecast = coordinates != null ? cache.GetValueOrDefault("weather") : null;
                var temperature = forecast?.Short.EndsWith('°') == true ? forecast.Short : "";
                var weekday = now.ToString("ddd", Portuguese).TrimEnd('.').ToUpper(Portuguese);
                var date = now.ToString("dd MMM", Portuguese).TrimEnd('.');
                Publish(id, new(now.ToString("HH:mm"), now.ToString("F", Portuguese) + (forecast == null ? "" : "\n" + forecast.Detail),
                    Card: new(now.ToString("HH:mm"), temperature.Length == 0 ? "" : $"Clima {temperature}", weekday, date)));
                return;
            case "agenda":
                try
                {
                    var appointment = agenda.Next(DateTimeOffset.Now);
                    var day = appointment?.Start.LocalDateTime ?? now;
                    var title = appointment?.Title ?? "Agenda livre";
                    var when = appointment == null ? "Adicionar evento" : appointment.Start.ToLocalTime().ToString("HH:mm");
                    var detail = appointment == null ? "Nenhum próximo compromisso." : day.ToString("dddd, dd 'de' MMMM · HH:mm", Portuguese);
                    Publish(id, new(title, detail, Card:
                        new(title, when, day.ToString("MMM", Portuguese).TrimEnd('.').ToUpper(Portuguese), day.ToString("dd"))));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
                { Publish(id, Reading.Missing("Não foi possível carregar a agenda local.")); }
                return;
            case "clock": Publish(id, new(now.ToString("HH:mm"), now.ToString("F", Portuguese))); return;
            case "calendar": Publish(id, new(now.ToString("dd MMM", Portuguese), now.ToString("D", Portuguese))); return;
            case "notes": Publish(id, new("Notas", "Abrir bloco de notas local")); return;
            case "stopwatch":
                var elapsed = stopwatch.Elapsed;
                Publish(id, new($"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}", stopwatch.IsRunning ? "Cronômetro em execução" : "Cronômetro parado")); return;
            case "pomodoro":
                var remaining = TimeSpan.FromSeconds(Math.Ceiling(focus.Remaining.TotalSeconds));
                Publish(id, new($"{remaining.Minutes:00}:{remaining.Seconds:00}", focus.Finished ? $"{focus.Mode} concluído" : focus.Mode)); return;
            case "weather" when coordinates is null:
                Publish(id, new("Clima", "Escolha as coordenadas para consultar o Open-Meteo.")); return;
            case "ai-usage" when !usage.Connected:
                Publish(id, new("Uso IA", "Conecte a conta administrativa para consultar tokens da API.")); return;
            case "gpu" or "temperature" when sensors is null:
                Publish(id, new(id == "gpu" ? "—" : "Temp.", "Abra para ativar a leitura dos sensores.")); return;
        }
        var key = id is "gpu" or "temperature" ? "sensors" : id;
        var cadence = key switch { "weather" or "ai-usage" => 900, "disk" or "battery" or "power-card" => 30, "sensors" or "music" => 5, _ => 2 };
        if (sampling.Contains(key) || (lastRead.TryGetValue(key, out var last) && Stopwatch.GetElapsedTime(last).TotalSeconds < cadence)) return;
        sampling.Add(key);
        lastRead[key] = Stopwatch.GetTimestamp();
        _ = SampleAsync(key);
    }

    private async Task SampleAsync(string key)
    {
        var sampledCoordinates = coordinates;
        var revision = weatherRevision;
        var sampledUsageRevision = usageRevision;
        var sampledSensors = sensors;
        try
        {
            if (key == "ai-usage")
            {
                var reading = await Task.Run(() => usage.ReadAsync(http, DateTimeOffset.UtcNow, lifetime.Token));
                if (sampledUsageRevision == usageRevision) Publish(key, reading);
            }
            else if (key == "sensors" && sampledSensors != null)
            {
                var result = await Task.Run(sampledSensors.Read);
                if (ReferenceEquals(sensors, sampledSensors)) { Publish("gpu", result.Gpu); Publish("temperature", result.Temperature); }
            }
            else if (key == "weather" && sampledCoordinates is { } location)
            {
                var reading = await Weather.ReadAsync(http, location.Latitude, location.Longitude, lifetime.Token);
                if (revision == weatherRevision) Publish(key, reading);
            }
            else
            {
                var reading = await Task.Run(() => key switch
                {
                    "cpu" => metrics.Cpu(), "ram" => SystemReadings.Memory(), "disk" => SystemReadings.Disk(),
                    "network" => metrics.Network(), "battery" => SystemReadings.Battery(),
                    "power-card" => SystemReadings.Power(),
                    "music" => Audio.ActiveSession(), "volume" => Audio.Reading(false), "microphone" => Audio.Reading(true),
                    _ => Reading.Missing("Leitura indisponível.")
                });
                Publish(key, reading);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (disposed) return;
            var error = Reading.Missing($"Leitura indisponível · {ex.GetBaseException().Message}");
            if (key == "sensors")
            {
                if (ReferenceEquals(sensors, sampledSensors)) { Publish("gpu", error); Publish("temperature", error); }
            }
            else if (key == "ai-usage")
            {
                if (sampledUsageRevision == usageRevision)
                    Publish(key, Reading.Missing("Não foi possível ler as credenciais ou consultar a API de uso."));
            }
            else if (key != "weather" || revision == weatherRevision) Publish(key, error);
        }
        finally { sampling.Remove(key); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Stop();
        PauseScreenTime();
        DisposeCodexLimits();
        lifetime.Cancel();
        foreach (var panel in panels.Values.ToArray()) panel.Close();
        tiles.Clear();
        sensors?.Dispose();
        http.Dispose();
        lifetime.Dispose();
    }
}
