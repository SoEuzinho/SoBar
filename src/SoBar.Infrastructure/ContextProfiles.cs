using System.Text.Json;
using SoBar.Core;

namespace SoBar.Infrastructure;

public sealed record ContextRule(string Monitor, string Executable, string Profile)
{
    internal bool SameCondition(string monitor, string executable) =>
        string.Equals(Monitor, monitor, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Executable, executable, StringComparison.OrdinalIgnoreCase);

    internal bool Valid => Monitor is { Length: <= 200 } && !Monitor.Any(char.IsControl)
        && Executable != null && (Executable.Length == 0 || AppSettings.IsLocalApplication(Executable))
        && (Executable.Length > 0 || Monitor.Length > 0)
        && !string.IsNullOrWhiteSpace(Profile) && Profile.Length <= 60 && !Profile.Any(char.IsControl);

    // A specific application wins over a monitor default. A combined match wins over both.
    internal int Priority => (Monitor.Length > 0 ? 1 : 0) + (Executable.Length > 0 ? 2 : 0);
}

public sealed class ContextProfiles
{
    private readonly List<(ContextRule Rule, AppSettings Settings)> entries;
    public AppSettings Default { get; }

    internal ContextProfiles(AppSettings fallback, List<(ContextRule Rule, AppSettings Settings)> entries)
    {
        Default = fallback;
        this.entries = entries.OrderByDescending(e => e.Rule.Priority).ToList();
    }

    public AppSettings Resolve(string monitor, string? foregroundMonitor, string? executable)
    {
        foreach (var entry in entries)
        {
            var rule = entry.Rule;
            if (rule.Monitor.Length > 0 && !string.Equals(monitor, rule.Monitor, StringComparison.OrdinalIgnoreCase)) continue;
            if (rule.Executable.Length > 0 &&
                (!string.Equals(foregroundMonitor, monitor, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(executable, rule.Executable, StringComparison.OrdinalIgnoreCase))) continue;
            return entry.Settings;
        }
        return Default;
    }
}

public sealed partial class AppServices
{
    public ContextRule[] ContextRules()
    {
        var value = Get("context", "rules");
        if (value == null) return [];
        try
        {
            var rules = JsonSerializer.Deserialize<ContextRule?[]>(value, Json) ?? [];
            return rules.OfType<ContextRule>().Where(r => r.Valid).Take(128).ToArray();
        }
        catch (JsonException)
        {
            _log.Warning("Regras contextuais inválidas ignoradas");
            return [];
        }
    }

    public void SetContextRule(string monitor, string executable, string profile)
    {
        var rule = new ContextRule(monitor.Trim(), executable.Trim(), profile.Trim());
        if (!rule.Valid) throw new ArgumentException("Selecione um monitor ou um arquivo .exe e um perfil salvo.");
        lock (_sync)
        {
            if (Get("profile", rule.Profile) == null) throw new InvalidDataException("Perfil desconhecido.");
            var rules = ContextRules().Where(r => !r.SameCondition(rule.Monitor, rule.Executable)).Append(rule).ToArray();
            if (rules.Length > 128) throw new InvalidDataException("O máximo é de 128 regras contextuais.");
            Put("context", "rules", JsonSerializer.Serialize(rules, Json));
        }
    }

    public void RemoveContextRule(string monitor, string executable)
    {
        lock (_sync)
        {
            var rules = ContextRules().Where(r => !r.SameCondition(monitor, executable)).ToArray();
            Put("context", "rules", JsonSerializer.Serialize(rules, Json));
        }
    }

    public ContextProfiles ReadContextProfiles()
    {
        lock (_sync)
        {
            // Decode once on an explicit settings change. Focus changes only inspect this in-memory plan.
            var entries = new List<(ContextRule, AppSettings)>();
            var profiles = new Dictionary<string, AppSettings>(StringComparer.Ordinal);
            foreach (var rule in ContextRules())
            {
                if (!profiles.TryGetValue(rule.Profile, out var settings))
                {
                    var json = Get("profile", rule.Profile);
                    if (json == null) continue;
                    try
                    {
                        var profile = Decode(json);
                        settings = Settings;
                        settings.Theme = profile.Theme;
                        settings.Accent = profile.Accent;
                        settings.Opacity = profile.Opacity;
                        settings.Size = profile.Size;
                        settings.Widgets = profile.Widgets;
                        settings.WidgetScales = profile.WidgetScales;
                        settings.WidgetGroups = profile.WidgetGroups;
                        profiles.Add(rule.Profile, settings);
                    }
                    catch (Exception e) when (e is InvalidDataException or JsonException)
                    {
                        _log.Warning("Perfil contextual inválido ignorado");
                        continue;
                    }
                }
                entries.Add((rule, settings));
            }
            return new ContextProfiles(Settings, entries);
        }
    }
}
