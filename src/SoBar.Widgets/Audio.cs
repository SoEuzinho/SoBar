using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoBar.Widgets;

internal static class Audio
{
    internal sealed record Level(float Volume, bool Muted, string Device);

    public static Level Read(bool capture)
    {
        using var devices = new MMDeviceEnumerator();
        using var device = devices.GetDefaultAudioEndpoint(capture ? DataFlow.Capture : DataFlow.Render, Role.Multimedia);
        return new(device.AudioEndpointVolume.MasterVolumeLevelScalar, device.AudioEndpointVolume.Mute, device.FriendlyName);
    }

    public static Reading Reading(bool capture)
    {
        var level = Read(capture);
        var status = level.Muted ? "Mudo" : $"{level.Volume:P0}";
        return new(status, $"{level.Device} · {status}");
    }

    public static void Change(bool capture, float? volume, bool? mute)
    {
        using var devices = new MMDeviceEnumerator();
        using var device = devices.GetDefaultAudioEndpoint(capture ? DataFlow.Capture : DataFlow.Render, Role.Multimedia);
        if (volume.HasValue) device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(volume.Value, 0, 1);
        if (mute.HasValue) device.AudioEndpointVolume.Mute = mute.Value;
    }

    public static Reading ActiveSession()
    {
        using var devices = new MMDeviceEnumerator();
        using var device = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = device.AudioSessionManager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
        {
            using var session = sessions[i];
            if (session.State != NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive || session.GetProcessID == 0) continue;
            try
            {
                using var source = Process.GetProcessById(checked((int)session.GetProcessID));
                return new(source.ProcessName, $"Aplicativo com sessão de áudio ativa · {source.ProcessName}");
            }
            catch (ArgumentException) { /* The session's process exited. */ }
        }
        return new("Música", "Nenhuma sessão de reprodução ativa.");
    }

    internal static void MediaCommand(nint window, int appCommand)
    {
        // Let the shell route the standard media command to the registered player.
        DefWindowProc(window, 0x0319, window, appCommand << 16);
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProc(nint hwnd, uint message, nint wparam, nint lparam);
}
