using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SoBar.Widgets;

internal sealed record CardLines(string First, string Second, string LeftTop = "", string LeftBottom = "", double? Upper = null, double? Lower = null);

// One visual tree per tile. Sampling only changes text and meter widths.
internal sealed class DockCard : Border
{
    private readonly TextBlock first, second, leftTop, leftBottom;
    private readonly Border[] meters = new Border[2];
    private readonly TextBlock[] meterLabels = new TextBlock[2];
    private readonly string id;
    internal string? ClockTime => id == "clock-card" ? first.Text : null;
    internal static bool Supports(string id) => id is "system-card" or "agenda" or "power-card" or "clock-card" or "ai-usage" or "codex-limits";

    internal DockCard(string id)
    {
        this.id = id;
        var power = id == "power-card";
        Width = id switch { "system-card" => 151, "codex-limits" => 166, "agenda" => 161, "power-card" => 65, "clock-card" => 143, _ => 123 };
        Height = 42;
        CornerRadius = new CornerRadius(10);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(power ? 6 : 9, 2, power ? 6 : 9, 2);
        if (power)
        {
            Background = new LinearGradientBrush(Color.FromRgb(49,169,255), Color.FromRgb(19,100,220), 90);
            BorderBrush = Brush("#599FFF");
        }
        else
        {
            Background = Brush("#1E2937");
            BorderBrush = Brush("#435064");
            if (Application.Current?.TryFindResource("DockTile") != null) SetResourceReference(BackgroundProperty, "DockTile");
            if (Application.Current?.TryFindResource("DockEdge") != null) SetResourceReference(BorderBrushProperty, "DockEdge");
        }
        var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        Child = grid;
        first = Text(id == "clock-card" ? 16 : 12, true);
        second = Text(id == "clock-card" ? 10 : 10.5, false);
        leftTop = Text(9, true);
        leftBottom = Text(id == "clock-card" ? 14 : 17, true);
        if (power)
        {
            var icon = Icon("\uE945", 14);
            icon.Foreground = Brushes.White;
            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            first.FontSize = 12;
            first.Foreground = Brushes.White;
            first.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(icon);
            content.Children.Add(first);
            grid.Children.Add(content);
            return;
        }
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (id is "system-card" or "codex-limits")
        {
            var processor = Icon(id == "system-card" ? "\uE950" : "\uE943", 20);
            processor.Margin = new Thickness(0, 0, 9, 0);
            grid.Children.Add(processor);
            var rows = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(rows, 1);
            grid.Children.Add(rows);
            MeterRow(rows, id == "system-card" ? "CPU" : "Sessão", first, 0);
            MeterRow(rows, id == "system-card" ? "RAM" : "Semana", second, 1);
            return;
        }

        if (id == "ai-usage")
        {
            var chip = new Border { CornerRadius = new CornerRadius(7), Width=27, Height=27, Margin=new Thickness(0,0,6,0),
                Background = Brush("#A95F39"), Child = Icon("\uE945", 15) };
            grid.Children.Add(chip);
        }
        else
        {
            var date = new StackPanel { Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            leftTop.Foreground = Brush("#FA8F80");
            leftTop.MinWidth = 27;
            date.Children.Add(leftTop);
            date.Children.Add(leftBottom);
            grid.Children.Add(date);
        }
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        if (id == "clock-card")
        {
            text.HorizontalAlignment = HorizontalAlignment.Right;
            first.Tag = "clock-time";
            second.HorizontalAlignment = HorizontalAlignment.Right;
        }
        text.Children.Add(first);
        text.Children.Add(second);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
    }

    private void MeterRow(StackPanel host, string name, TextBlock value, int index)
    {
        var row = new Grid { Height = 15 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(id == "codex-limits" ? 40 : 27) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(31) });
        meterLabels[index] = new TextBlock { Text = name, FontSize = 9.5, FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(meterLabels[index]);
        var track = new Border { Height = 3, Background = Brush("#4C565E"), CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
        var fill = new Border { Background = Brush(index == 0 ? "#5DECA0" : "#63C7A7"), CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left };
        meters[index] = fill;
        track.Child = fill;
        Grid.SetColumn(track,1); row.Children.Add(track);
        value.FontSize = 9.5;
        value.FontWeight = FontWeights.SemiBold;
        value.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(value,2); row.Children.Add(value);
        track.SizeChanged += (_,_) => UpdateFill(fill, track.ActualWidth);
        host.Children.Add(row);
    }

    private static TextBlock Text(double size, bool bold) => new()
    {
        TextTrimming = TextTrimming.CharacterEllipsis, FontSize = size,
        VerticalAlignment = VerticalAlignment.Center, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal
    };

    private static TextBlock Icon(string glyph, double size) => new()
    {
        Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = size,
        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
    };

    private static SolidColorBrush Brush(string hex)
    {
        var result = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        result.Freeze();
        return result;
    }

    internal void Set(Reading reading)
    {
        var content = reading.Card ?? new CardLines(reading.Short, "");
        if (id == "ai-usage") content = new("Uso da API", reading.Short == "Uso IA" ? "Conectar conta" : reading.Short);
        first.Text = content.First;
        second.Text = content.Second;
        second.Visibility = content.Second.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        leftTop.Text = content.LeftTop;
        leftBottom.Text = content.LeftBottom;
        if (id is not ("system-card" or "codex-limits")) return;
        if (id == "codex-limits")
        {
            meterLabels[0].Text = content.LeftTop;
            meterLabels[1].Text = content.LeftBottom;
        }
        for (int i = 0; i < 2; i++)
        {
            var fill = meters[i];
            fill.Tag = i == 0 ? content.Upper : content.Lower;
            UpdateFill(fill, ((FrameworkElement)fill.Parent).ActualWidth);
        }
    }

    private static void UpdateFill(Border fill, double width) =>
        fill.Width = fill.Tag is double value ? Math.Max(0, width * Math.Clamp(value, 0, 100) / 100d) : 0;
}
