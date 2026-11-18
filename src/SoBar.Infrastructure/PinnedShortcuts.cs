using System.Runtime.InteropServices;
using SoBar.Core;

namespace SoBar.Infrastructure;

public static class PinnedShortcuts
{
    public static List<string> Discover()
    {
        var apps = new List<string>();
        void AddLocal(string path)
        {
            if (AppSettings.IsLocalApplication(path) && File.Exists(path) &&
                !apps.Contains(path, StringComparer.OrdinalIgnoreCase)) apps.Add(path);
        }

        AddLocal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (!Directory.Exists(directory) || shellType == null) return apps;

        object? shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell == null) return apps;
            foreach (var file in Directory.EnumerateFiles(directory, "*.lnk").Take(32))
            {
                object? shortcut = null;
                try
                {
                    shortcut = ((dynamic)shell).CreateShortcut(file);
                    dynamic link = shortcut;
                    // A shortcut with arguments cannot be represented by an executable-only pin.
                    if (string.IsNullOrWhiteSpace((string?)link.Arguments))
                        AddLocal(Environment.ExpandEnvironmentVariables((string?)link.TargetPath ?? ""));
                }
                catch (COMException) { }
                finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); }
            }
        }
        catch (Exception error) when (error is COMException or IOException or UnauthorizedAccessException) { }
        finally { if (shell != null) Marshal.FinalReleaseComObject(shell); }
        return apps;
    }
}
