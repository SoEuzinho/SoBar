using System.Diagnostics;
using System.Text;
using SoBar.Plugins;

namespace SoBar.Infrastructure;

public static class PluginBroker
{
    private const int MaxBytes = 8192;
    public static int RunWorker()
    {
        try
        {
            var buffer = new byte[MaxBytes + 1];
            using var input = Console.OpenStandardInput();
            var length = input.ReadAtLeast(buffer,buffer.Length,throwOnEndOfStream:false);
            if (length is 0 or > MaxBytes) return 2;
            var manifest = ExtensionManifest.FromStored(Encoding.UTF8.GetString(buffer,0,length).TrimStart('\uFEFF'));
            using var output = Console.OpenStandardOutput();
            output.Write(Encoding.UTF8.GetBytes(manifest.Serialize()));
            return 0;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or System.Text.Json.JsonException) { return 2; }
    }

    internal static ExtensionManifest Inspect(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length is 0 or > MaxBytes) throw new InvalidDataException("O manifesto deve ter até 8 KiB.");
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Host indisponível.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, StandardOutputEncoding = Encoding.UTF8 };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(System.Reflection.Assembly.GetEntryAssembly()!.Location);
        start.ArgumentList.Add("--inspect-plugin");
        using var worker = Process.Start(start) ?? throw new IOException("O host de extensões não iniciou.");
        try
        {
            var output = worker.StandardOutput.ReadToEndAsync();
            file.CopyTo(worker.StandardInput.BaseStream);
            worker.StandardInput.Close();
            if (!worker.WaitForExit(8000)) throw new TimeoutException("A extensão excedeu o tempo de validação.");
            if (worker.ExitCode != 0) throw new InvalidDataException("Manifesto incompatível ou permissões inválidas.");
            return ExtensionManifest.FromStored(output.GetAwaiter().GetResult());
        }
        finally { if (!worker.HasExited) worker.Kill(); }
    }
}
