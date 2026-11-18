using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace SoBar.Widgets;

internal sealed record Reading(string Short, string Detail, double? Percent = null, CardLines? Card = null)
{
    public static Reading Missing(string reason) => new("—", reason);
}

internal sealed class SystemReadings
{
    private (ulong Idle, ulong Total)? previousCpu;
    private Dictionary<string, (long Rx, long Tx)> previousNetwork = [];
    private long networkTimestamp;

    public Reading Cpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return Reading.Missing("A API do processador está indisponível.");
        var total = kernel + user;
        var value = previousCpu is { } last ? CpuPercent(last.Idle, last.Total, idle, total) : null;
        previousCpu = (idle, total);
        return value is double percent
            ? new($"{percent:F0}%", $"Uso total do processador · {Environment.ProcessorCount} processadores lógicos", percent)
            : Reading.Missing("Aguardando a próxima amostra do processador.");
    }

    internal static double? CpuPercent(ulong oldIdle, ulong oldTotal, ulong idle, ulong total)
    {
        if (total <= oldTotal || idle < oldIdle) return null;
        var elapsed = total - oldTotal;
        var idleDelta = idle - oldIdle;
        if (idleDelta > elapsed) return null;
        return 100d * (elapsed - idleDelta) / elapsed;
    }

    public static Reading Memory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status)) return Reading.Missing("Memória física indisponível.");
        return new($"{status.Load}%", $"{GiB(status.TotalPhysical - status.AvailablePhysical):F1} de {GiB(status.TotalPhysical):F1} GiB em uso", status.Load);
    }

    public static Reading Disk()
    {
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        return new($"{GiB(drive.AvailableFreeSpace):F0} GiB", $"{drive.Name} · {GiB(drive.AvailableFreeSpace):F1} GiB disponíveis de {GiB(drive.TotalSize):F1} GiB");
    }

    private static double GiB(ulong bytes) => bytes / 1073741824d;
    private static double GiB(long bytes) => bytes / 1073741824d;

    public Reading Network()
    {
        long rx = 0, tx = 0;
        bool comparable = false;
        var current = new Dictionary<string, (long Rx, long Tx)>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;
            var stats = adapter.GetIPStatistics();
            current[adapter.Id] = (stats.BytesReceived, stats.BytesSent);
            if (previousNetwork.TryGetValue(adapter.Id, out var old) && stats.BytesReceived >= old.Rx && stats.BytesSent >= old.Tx)
            {
                rx += stats.BytesReceived - old.Rx;
                tx += stats.BytesSent - old.Tx;
                comparable = true;
            }
        }
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(networkTimestamp, now).TotalSeconds;
        networkTimestamp = now;
        previousNetwork = current;
        if (!comparable || elapsed <= 0) return Reading.Missing(current.Count == 0 ? "Nenhuma interface de rede ativa." : "Aguardando a próxima amostra de rede.");
        return new($"↓ {Rate(rx / elapsed)}", $"Recepção {Rate(rx / elapsed)} · Envio {Rate(tx / elapsed)}");
    }

    private static string Rate(double value) => value >= 1048576 ? $"{value / 1048576:F1} MiB/s" : $"{value / 1024:F0} KiB/s";

    public static Reading Battery()
    {
        if (!GetSystemPowerStatus(out var status) || status.BatteryFlag == 255 || (status.BatteryFlag & 128) != 0 || status.Percent > 100)
            return Reading.Missing("Nenhuma bateria com leitura disponível.");
        var source = status.Ac == 1 ? "Na tomada" : status.Ac == 0 ? "Usando a bateria" : "Fonte de energia desconhecida";
        return new($"{status.Percent}%", $"{source} · {status.Percent}% de carga");
    }

    public static Reading Power() => GetSystemPowerStatus(out var status)
        ? FormatPower(status.Ac, status.BatteryFlag, status.Percent)
        : Reading.Missing("O Windows não disponibilizou o estado da energia.");

    internal static Reading FormatPower(byte mains, byte flag, byte percent)
    {
        if (flag != 255 && (flag & 128) == 0 && percent <= 100)
        {
            var state = (flag & 8) != 0 ? "Carregando" : mains == 1 ? "Na tomada" : "Carga da bateria";
            return new($"{percent}%", $"{state} · {percent}% de bateria", percent);
        }
        if (mains == 1) return new("Tomada", "O Windows informa alimentação pela tomada.");
        if (mains == 0) return new("Bateria", "O Windows informa alimentação por bateria, sem percentual disponível.");
        return Reading.Missing("Fonte de energia desconhecida.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus
    {
        public byte Ac, BatteryFlag, Percent, SystemStatus;
        public int BatteryLife, FullLife;
    }

    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out PowerStatus status);
}
