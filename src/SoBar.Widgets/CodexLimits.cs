using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SoBar.Widgets;

internal static class CodexLimits
{
    internal static bool IsExecutable(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) &&
        path.Length > 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' &&
        Path.GetFileName(path).Equals("codex.exe",StringComparison.OrdinalIgnoreCase) && File.Exists(path);

    internal static string? Locate()
    {
        var searchPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var entry in searchPath.Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries))
        {
            var folder = entry.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(folder)) continue;
            var path = Path.Combine(folder,"codex.exe");
            if (IsExecutable(path)) return path;
        }
        return null;
    }

    internal static async Task<Reading> QueryAsync(string executable,CancellationToken token)
    {
        if (!IsExecutable(executable)) throw new FileNotFoundException("Localize novamente o executável do Codex.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = Environment.SystemDirectory
        };
        start.ArgumentList.Add("app-server");
        using var process = Process.Start(start) ?? throw new IOException("Falha ao iniciar o Codex.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var drain = process.StandardError.BaseStream.CopyToAsync(Stream.Null,deadline.Token);
        try
        {
            return await ExchangeAsync(process.StandardInput,process.StandardOutput,deadline.Token).ConfigureAwait(false);
        }
        finally
        {
            process.StandardInput.Close();
            if (!process.HasExited) process.Kill(entireProcessTree:true);
            try { await drain.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
    }

    internal static async Task<Reading> ExchangeAsync(TextWriter input, TextReader output, CancellationToken token)
    {
        async Task Send(string json)
        {
            await input.WriteLineAsync(json.AsMemory(), token).ConfigureAwait(false);
            await input.FlushAsync(token).ConfigureAwait(false);
        }
        async Task<JsonElement> Receive(int id)
        {
            while (true)
            {
                var line = await output.ReadLineAsync(token).ConfigureAwait(false);
                if (line == null) throw new IOException("A sessão do Codex foi encerrada.");
                using var message = JsonDocument.Parse(line);
                var root = message.RootElement;
                if (!root.TryGetProperty("id",out var number) || number.ValueKind != JsonValueKind.Number ||
                    !number.TryGetInt32(out var responseId) || responseId != id) continue;
                if (root.TryGetProperty("error",out _) || !root.TryGetProperty("result",out var result))
                    throw new InvalidDataException("O Codex não disponibilizou os limites da conta.");
                return result.Clone();
            }
        }
        await Send("""{"id":1,"method":"initialize","params":{"clientInfo":{"name":"sobar","title":"SoBar","version":"0.2.0"}}}""");
        await Receive(1);
        await Send("""{"method":"initialized","params":{}}""");
        await Send("""{"id":2,"method":"account/rateLimits/read"}""");
        return Parse(await Receive(2));
    }

    internal static Reading Parse(JsonElement result)
    {
        JsonElement bucket;
        if (result.TryGetProperty("rateLimitsByLimitId",out var map) && map.ValueKind == JsonValueKind.Object &&
            map.TryGetProperty("codex",out var codex)) bucket = codex;
        else if (!result.TryGetProperty("rateLimits",out bucket) || bucket.ValueKind != JsonValueKind.Object ||
            (bucket.TryGetProperty("limitId",out var name) && name.ValueKind == JsonValueKind.String && name.GetString() != "codex"))
            throw new InvalidDataException("A conta não informou limites do Codex.");
        (string Label,string Value,double? Percent,string Info) Window(string property,string fallback)
        {
            if (!bucket.TryGetProperty(property,out var item) || item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("usedPercent",out var used) || used.ValueKind != JsonValueKind.Number ||
                !used.TryGetDouble(out var pct) || !double.IsFinite(pct) || pct < 0 || pct > 100)
                return (fallback,"—",null,$"{fallback}: indisponível.");
            var label = fallback;
            if (item.TryGetProperty("windowDurationMins",out var duration) && duration.ValueKind == JsonValueKind.Number &&
                duration.TryGetInt32(out var minutes) && minutes > 0)
                label = minutes switch { 10080 => "Semana", 300 => "5 h", _ when minutes % 60 == 0 => $"{minutes / 60} h", _ => $"{minutes} min" };
            var info = $"{label}: {pct:0}% usados.";
            if (item.TryGetProperty("resetsAt",out var reset) && reset.ValueKind == JsonValueKind.Number &&
                reset.TryGetInt64(out long unix) && unix is > 0 and <= 253402300799)
                info += $" Renovação em {DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime():dd/MM HH:mm}.";
            return (label,$"{pct:0}%",pct,info);
        }
        var first = Window("primary","Sessão");
        var second = Window("secondary","Semana");
        if (first.Percent == null && second.Percent == null) throw new InvalidDataException("A conta não informou janelas de uso.");
        return new("Limites do Codex",first.Info + "\n" + second.Info,
            Card:new(first.Value,second.Value,first.Label,second.Label,first.Percent,second.Percent));
    }
}
