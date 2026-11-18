using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace SoBar.Widgets;

internal static class Weather
{
    internal static bool TryCoordinates(string latitude, string longitude, out double lat, out double lon)
    {
        static double Parse(string s) => double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var result) ? result : double.NaN;
        lat = Parse(latitude);
        lon = Parse(longitude);
        return lat is >= -90 and <= 90 && lon is >= -180 and <= 180;
    }

    public static async Task<Reading> ReadAsync(HttpClient client, double latitude, double longitude, CancellationToken cancellation)
    {
        var url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude:F4}&longitude={longitude:F4}&current=temperature_2m,weather_code&timezone=auto");
        using var stream = await client.GetStreamAsync(url, cancellation).ConfigureAwait(false);
        using var data = await JsonDocument.ParseAsync(stream, cancellationToken: cancellation).ConfigureAwait(false);
        var current = data.RootElement.GetProperty("current");
        double temp = current.GetProperty("temperature_2m").GetDouble();
        var condition = current.GetProperty("weather_code").GetInt32() switch
        {
            0 => "Céu limpo", 1 or 2 => "Parcialmente nublado", 3 => "Encoberto", 45 or 48 => "Nevoeiro",
            >= 51 and <= 57 => "Garoa", >= 61 and <= 67 => "Chuva", >= 71 and <= 77 => "Neve",
            >= 80 and <= 82 => "Pancadas de chuva", 85 or 86 => "Pancadas de neve", >= 95 and <= 99 => "Trovoadas", _ => "Condição indisponível"
        };
        return new($"{temp:F0}°", $"{condition} · {temp:F1} °C\nOpen-Meteo · {current.GetProperty("time").GetString()?.Replace('T', ' ')} (hora local)");
    }
}
