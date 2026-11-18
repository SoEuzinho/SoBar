using System.Text.RegularExpressions;

namespace SoBar.Core;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Escuro";
    public string Accent { get; set; } = "#57D9AF";
    public double Opacity { get; set; } = .96;
    public string Edge { get; set; } = "Bottom";
    public string Size { get; set; } = "Normal";
    public Dictionary<string, double> WidgetScales { get; set; } = [];
    public string MonitorId { get; set; } = "";
    public bool AllMonitors { get; set; }
    public bool ReserveSpace { get; set; }
    public bool AutoHide { get; set; }
    public bool HideFullscreen { get; set; } = true;
    public bool GroupWindows { get; set; }
    public bool StartWithWindows { get; set; }
    public bool HistoryEnabled { get; set; }
    public string Shortcut { get; set; } = "Ctrl+Alt+Space";
    public List<string> Widgets { get; set; } = ["clock", "cpu", "ram"];
    public List<string> PinnedApps { get; set; } = [];

    public void Validate()
    {
        if (Theme is not ("Escuro" or "Claro" or "Liquid Glass" or "iOS Glass" or "Midnight") || Edge is not ("Top" or "Bottom" or "Left" or "Right"))
            throw new InvalidDataException("Tema ou posição desconhecida.");
        if (Size is not ("Compacto" or "Normal" or "Grande") || WidgetScales is null || WidgetScales.Count > 64 ||
            WidgetScales.Any(kv => kv.Key.Length > 80 || !double.IsFinite(kv.Value) || kv.Value is < .75 or > 1.5))
            throw new InvalidDataException("Tamanho de barra ou widget inválido.");
        if (Accent is null || !Regex.IsMatch(Accent, "^#[a-fA-F0-9]{6}$") || !double.IsFinite(Opacity) || Opacity is < .2 or > 1)
            throw new InvalidDataException("Cor ou opacidade inválida.");
        if (MonitorId is null || MonitorId.Length > 200 || string.IsNullOrWhiteSpace(Shortcut) || Shortcut.Length > 80)
            throw new InvalidDataException("Monitor ou atalho inválido.");
        if (Widgets is null || Widgets.Count > 64 || Widgets.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 80) ||
            PinnedApps is null || PinnedApps.Count > 128 || PinnedApps.Any(x => !IsLocalApplication(x)))
            throw new InvalidDataException("Lista de widgets ou aplicativos inválida.");
        Widgets = Widgets.Distinct(StringComparer.Ordinal).ToList();
        PinnedApps = PinnedApps.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool IsLocalApplication(string? path) => !string.IsNullOrWhiteSpace(path) &&
        path.Length <= 32767 && Path.IsPathFullyQualified(path) && char.IsAsciiLetter(path[0]) &&
        path[1] == ':' && Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase);
}

public sealed record SearchHit(string Name, string Path, string Kind);
public sealed record AssistantReply(string Text, string? SuggestedAction);
public sealed record PluginTile(string Id, string Name, string Value);
