using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SoBar.Core;

namespace SoBar.Plugins;

// Declarative capabilities are the entire extension language. No CLR type names,
// paths, markup, templates, native code or expressions are resolved from a manifest.
public sealed record ExtensionManifest(int ApiVersion, string Id, string Name, string Source,
    string[] Permissions, string? Text = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 5, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static ExtensionManifest Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is 0 or > 8192) throw new InvalidDataException("O manifesto deve ter até 8 KiB.");
        var manifest = JsonSerializer.Deserialize<ExtensionManifest>(stream, Json)
            ?? throw new InvalidDataException("Manifesto vazio.");
        manifest.Validate();
        return manifest;
    }

    public void Validate()
    {
        if (ApiVersion != 1 || Id is null || !Regex.IsMatch(Id, @"\A[a-z][a-z0-9-]{0,39}\z") ||
            string.IsNullOrWhiteSpace(Name) || Name.Length > 48 || Name.Any(char.IsControl))
            throw new InvalidDataException("Versão, identificador ou nome de extensão inválido.");
        var required = Source switch
        {
            "text" => Array.Empty<string>(), "utc" => ["clock.read"], "uptime" => ["uptime.read"],
            _ => throw new InvalidDataException("Fonte desconhecida.")
        };
        if (Permissions is null || !Permissions.SequenceEqual(required) || Text?.Length > 100 ||
            (Source == "text" && string.IsNullOrWhiteSpace(Text)) || Text?.Any(char.IsControl) == true)
            throw new InvalidDataException("Permissão ou texto inválido.");
    }

    public PluginTile Render()
    {
        var value = Source switch
        {
            "utc" => DateTime.UtcNow.ToString("HH:mm 'UTC'", CultureInfo.InvariantCulture),
            "uptime" => TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\d\ hh\h\ mm\m"),
            "text" => Text!, _ => throw new InvalidDataException()
        };
        return new PluginTile(Id, Name, value);
    }

    public string Serialize() => JsonSerializer.Serialize(this, Json);
    public static ExtensionManifest FromStored(string data)
    {
        var value = JsonSerializer.Deserialize<ExtensionManifest>(data, Json) ?? throw new InvalidDataException();
        value.Validate();
        return value;
    }
}
