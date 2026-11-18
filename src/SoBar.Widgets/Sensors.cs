using LibreHardwareMonitor.Hardware;

namespace SoBar.Widgets;

internal sealed class Sensors : IDisposable
{
    private Computer? computer;
    private bool disposed;
    private readonly object gate = new();

    public (Reading Gpu, Reading Temperature) Read()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (computer is null)
            {
                var instance = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
                try { instance.Open(); computer = instance; }
                catch { instance.Close(); throw; }
            }
            var gpu = new List<(float Value, string Name)>();
            var temperature = new List<(float Value, string Name)>();
            foreach (IHardware hardware in computer.Hardware)
                Visit(hardware, gpu, temperature);

            var gpuReading = gpu.Count == 0 ? Reading.Missing("O driver não expôs a carga da GPU.") : Format(gpu.MaxBy(x => x.Value), "%");
            var tempReading = temperature.Count == 0 ? Reading.Missing("Nenhum sensor de temperatura acessível.") : Format(temperature.MaxBy(x => x.Value), " °C");
            return (gpuReading, tempReading);
        }
    }

    private static Reading Format((float Value, string Name) sample, string unit) => new($"{sample.Value:F0}{unit}", $"{sample.Name} · {sample.Value:F1}{unit}");

    private static void Visit(IHardware hardware, List<(float, string)> gpu, List<(float, string)> temperature)
    {
        hardware.Update();
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not float value || !float.IsFinite(value)) continue;
            var name = $"{hardware.Name} · {sensor.Name}";
            if (sensor.SensorType == SensorType.Temperature) temperature.Add((value, name));
            if (sensor.SensorType == SensorType.Load &&
                hardware.HardwareType is HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia &&
                (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) || sensor.Name.Contains("D3D 3D", StringComparison.OrdinalIgnoreCase)))
                gpu.Add((value, name));
        }
        foreach (var child in hardware.SubHardware) Visit(child, gpu, temperature);
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            computer?.Close();
            computer = null;
        }
    }
}
