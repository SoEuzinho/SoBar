using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SoBar.Widgets;

internal sealed record UsageBucket(long UtcHour, string Application, double Seconds);
internal sealed record UsageSummary(double Seconds, double[] Hours, IReadOnlyList<(string Name, double Seconds)> Apps);

internal sealed class ScreenTimeStore(string directory)
{
    private const long HourSeconds = 3600;
    private readonly string ledgerPath = Path.Combine(directory, "screen-time.json");
    private readonly string consentPath = Path.Combine(directory, "screen-time.opt-in");
    private Dictionary<(long Hour, string App), double>? totals;
    private DateTimeOffset? lastTime;
    private string? previousApp;
    private bool dirty;
    private DateTimeOffset lastWrite;
    internal bool Consented { get; private set; } = File.Exists(Path.Combine(directory, "screen-time.opt-in"));

    internal void Consent(bool enable)
    {
        if (enable)
        {
            if (!Consented) { Directory.CreateDirectory(directory); File.WriteAllText(consentPath, "v1"); }
            Consented = true;
        }
        else
        {
            if (File.Exists(consentPath)) File.Delete(consentPath);
            Consented = false;
            Pause();
        }
    }

    internal void Clear()
    {
        ResetSampling();
        if (File.Exists(ledgerPath)) File.Delete(ledgerPath);
        totals = [];
        dirty = false;
    }

    internal void Pause()
    {
        ResetSampling();
        Flush();
    }

    internal void ResetSampling()
    {
        lastTime = null;
        previousApp = null;
    }

    internal void Observe(DateTimeOffset now, string? application)
    {
        if (!Consented || string.IsNullOrWhiteSpace(application)) { ResetSampling(); return; }
        var data = Load();
        if (previousApp == application && lastTime is { } last && now > last && now - last <= TimeSpan.FromSeconds(8))
        {
            // Split any interval crossing an hour boundary. Long gaps and focus transitions contribute no guessed time.
            while (last < now)
            {
                var local = last.ToLocalTime();
                long hour = local.AddTicks(-(local.TimeOfDay.Ticks % TimeSpan.TicksPerHour)).ToUnixTimeSeconds();
                var end = DateTimeOffset.FromUnixTimeSeconds(hour + HourSeconds);
                if (end > now) end = now;
                var key = (hour,application);
                data[key] = Math.Min(HourSeconds, data.GetValueOrDefault(key) + (end - last).TotalSeconds);
                last = end;
            }
            dirty = true;
        }
        lastTime = now;
        previousApp = application;
        if (now - lastWrite >= TimeSpan.FromSeconds(30))
        {
            Trim(now);
            Flush();
            lastWrite = now;
        }
    }

    internal UsageSummary Summary(DateTimeOffset now, int days)
    {
        if (days is not (1 or 7)) throw new ArgumentOutOfRangeException(nameof(days));
        var data = Load();
        Trim(now);
        var start = DateOnly.FromDateTime(now.LocalDateTime).AddDays(1-days);
        var hours = new double[24];
        var apps = new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        foreach (var ((hour, app), seconds) in data)
        {
            var localHour = DateTimeOffset.FromUnixTimeSeconds(hour).LocalDateTime;
            if (DateOnly.FromDateTime(localHour) < start || DateOnly.FromDateTime(localHour) > DateOnly.FromDateTime(now.LocalDateTime)) continue;
            hours[localHour.Hour] += seconds;
            apps[app] = apps.GetValueOrDefault(app) + seconds;
        }
        return new(hours.Sum(), hours, apps.OrderByDescending(a => a.Value).Select(a => (a.Key, a.Value)).ToArray());
    }

    private Dictionary<(long Hour, string App), double> Load()
    {
        if (totals != null) return totals;
        if (!File.Exists(ledgerPath)) return totals = [];
        using var file = File.OpenRead(ledgerPath);
        if (file.Length > 8_000_000) throw new InvalidDataException("Histórico de uso muito grande.");
        var rows = JsonSerializer.Deserialize<List<UsageBucket>>(file) ?? throw new InvalidDataException("Histórico de uso inválido.");
        var loaded = new Dictionary<(long,string),double>();
        foreach (var row in rows)
        {
            if (row is null || row.UtcHour < 0 || row.UtcHour > 253402297200 ||
                string.IsNullOrWhiteSpace(row.Application) || row.Application.Length > 260 || row.Seconds is < 0 or > HourSeconds ||
                !double.IsFinite(row.Seconds))
                throw new InvalidDataException("Histórico de uso inválido.");
            loaded[(row.UtcHour,row.Application)] = row.Seconds;
        }
        return totals = loaded;
    }

    private void Trim(DateTimeOffset now)
    {
        var data = Load();
        var firstDay = DateOnly.FromDateTime(now.LocalDateTime).AddDays(-6);
        foreach (var key in data.Keys.Where(k => DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(k.Hour).LocalDateTime) < firstDay).ToArray())
        { data.Remove(key); dirty = true; }
    }

    internal void Flush()
    {
        if (!dirty || totals == null) return;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".screen-time-{Guid.NewGuid():N}.tmp");
        try
        {
            var rows = totals.Select(e => new UsageBucket(e.Key.Hour,e.Key.App,e.Value));
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(rows));
            File.Move(temporary, ledgerPath, true);
            dirty = false;
        }
        finally { File.Delete(temporary); }
    }
}

internal static class ForegroundActivity
{
    public static string? App()
    {
        var info = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        if (!GetLastInputInfo(ref info) || unchecked((uint)Environment.TickCount - info.Tick) >= 60_000 || !InteractiveDesktop()) return null;
        var handle = GetForegroundWindow();
        if (handle == 0 || GetWindowThreadProcessId(handle,out uint pid) == 0 || pid == 0) return null;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            string name = process.ProcessName;
            return name is "LockApp" or "LogonUI" or "Idle" ? null : name;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return null; }
    }

    private static bool InteractiveDesktop()
    {
        var desktop = OpenInputDesktop(0, false, 0x0001);
        if (desktop == 0) return false;
        try
        {
            var buffer = new StringBuilder(64);
            return GetUserObjectInformation(desktop, 2, buffer, buffer.Capacity * 2, out _) &&
                buffer.ToString().Equals("Default",StringComparison.OrdinalIgnoreCase);
        }
        finally { CloseDesktop(desktop); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size, Tick; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInput lastInput);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd,out uint processId);
    [DllImport("user32.dll")] private static extern nint OpenInputDesktop(uint flags,bool inherit,uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern bool GetUserObjectInformation(nint handle,int index,StringBuilder output,int length,out int needed);
}
