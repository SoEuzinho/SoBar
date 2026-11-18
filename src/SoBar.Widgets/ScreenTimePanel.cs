using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace SoBar.Widgets;

public sealed partial class WidgetService
{
    private int usageDays = 1;
    private Action<UsageSummary>? drawUsage;
    private bool trackingSystemEvents;

    private void TrackSystemEvents(bool enable)
    {
        if (enable == trackingSystemEvents) return;
        if (enable)
        {
            SystemEvents.PowerModeChanged += OnPowerChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }
        else
        {
            SystemEvents.PowerModeChanged -= OnPowerChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
        }
        trackingSystemEvents = enable;
    }
    private void OnPowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Suspend or PowerModes.Resume) timer.Dispatcher.BeginInvoke(new Action(screenTime.ResetSampling));
    }
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) =>
        timer.Dispatcher.BeginInvoke(new Action(screenTime.ResetSampling));

    private static string DurationLabel(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours} h {time.Minutes:00} min";
        return time.TotalMinutes >= 1 ? $"{(int)time.TotalMinutes} min" : $"{time.Seconds} s";
    }

    private void RefreshScreenTime()
    {
        const string id = "screen-time";
        if (lastRead.TryGetValue(id, out var previous) && Stopwatch.GetElapsedTime(previous).TotalSeconds < 2) return;
        lastRead[id] = Stopwatch.GetTimestamp();
        try
        {
            TrackSystemEvents(screenTime.Consented && tiles.ContainsKey(id));
            if (trackingSystemEvents)
                screenTime.Observe(DateTimeOffset.Now, ForegroundActivity.App());
            else screenTime.ResetSampling();
            if (!screenTime.Consented && !panels.ContainsKey(id))
            {
                Publish(id, new("Tempo de tela", "Ative a coleta local para medir o tempo de uso."));
                return;
            }
            var today = screenTime.Summary(DateTimeOffset.Now, 1);
            Publish(id, new(screenTime.Consented ? DurationLabel(today.Seconds) : "Tempo de tela",
                screenTime.Consented ? "Tempo ativo de hoje · coleta local" : "Coleta pausada."));
            drawUsage?.Invoke(usageDays == 1 ? today : screenTime.Summary(DateTimeOffset.Now,7));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            screenTime.ResetSampling();
            Publish(id, Reading.Missing("Não foi possível ler ou salvar o histórico local."));
        }
    }

    private void PauseScreenTime()
    {
        TrackSystemEvents(false);
        try { screenTime.Pause(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { screenTime.ResetSampling(); }
    }

    private void BuildScreenTime(StackPanel body)
    {
        body.Children.Add(Paragraph("Ao ativar, o SoBar conta o tempo por aplicativo enquanto você usa o computador. Guarda no dispositivo o nome do processo, a hora e a duração por até sete dias."));
        var period = new ComboBox { ItemsSource = new[] { "Hoje", "Últimos 7 dias" }, SelectedIndex = usageDays == 1 ? 0 : 1 };
        AddFieldLabel(body, "Período", period);
        body.Children.Add(period);
        var chart = new System.Windows.Controls.Primitives.UniformGrid { Columns = 24, Rows = 1, Height = 42, Margin = new Thickness(0,10,0,2) };
        var bars = new Border[24];
        for (int i=0; i<bars.Length; i++)
        {
            bars[i] = new Border { Background=new SolidColorBrush(Color.FromRgb(86,169,244)),Margin=new Thickness(1,0,1,0),
                VerticalAlignment=VerticalAlignment.Bottom,CornerRadius=new CornerRadius(1) };
            chart.Children.Add(bars[i]);
        }
        body.Children.Add(chart);
        var axis = new DockPanel();
        var end = new TextBlock { Text = "23 h" };
        DockPanel.SetDock(end,Dock.Right); axis.Children.Add(end); axis.Children.Add(new TextBlock { Text="0 h" });
        body.Children.Add(axis);
        var total = Paragraph("");
        var apps = Paragraph("");
        body.Children.Add(total);
        body.Children.Add(new ScrollViewer { Content = apps, MaxHeight = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        drawUsage = summary =>
        {
            var peak = Math.Max(1, summary.Hours.Max());
            for(int i=0; i<bars.Length; i++)
            {
                bars[i].Height = 40 * summary.Hours[i] / peak;
                bars[i].ToolTip = $"{i:00} h · {DurationLabel(summary.Hours[i])}";
            }
            total.Text = $"Tempo ativo no período · {DurationLabel(summary.Seconds)}";
            apps.Text = summary.Apps.Count == 0 ? "Nenhuma atividade registrada." :
                string.Join("\n", summary.Apps.Select(a => $"{a.Name}   {DurationLabel(a.Seconds)}"));
        };
        var status = Paragraph("");
        void Update(Action? change = null)
        {
            try { change?.Invoke(); lastRead.Remove("screen-time"); RefreshScreenTime(); status.Text = screenTime.Consented ? "Coleta autorizada." : "Coleta pausada."; }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException) { status.Text = "Não foi possível alterar o histórico local."; }
        }
        period.SelectionChanged += (_,_) => { usageDays = period.SelectedIndex == 0 ? 1 : 7; Update(); };
        AddAction(body, "Autorizar coleta local", () => Update(() => screenTime.Consent(true)));
        AddAction(body, "Pausar coleta", () => Update(() => screenTime.Consent(false)));
        AddAction(body, "Apagar histórico", () =>
        {
            if (MessageBox.Show(Window.GetWindow(body),"Apagar o histórico de tempo de tela deste computador?","Tempo de tela",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No) == MessageBoxResult.Yes)
                Update(screenTime.Clear);
        });
        body.Children.Add(status);
        panels["screen-time"].Closed += (_,_) => drawUsage = null;
        Update();
    }
}
