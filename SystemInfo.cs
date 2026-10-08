using System.Diagnostics;
using LibreHardwareMonitor.Hardware;
using System.Management;

namespace DiscordSysInfoRPC;

public class HardwareData
{
    public string Name { get; set; } = "";
    public float Load { get; set; }
    public float? Temperature { get; set; }
    public float? Speed { get; set; }
}

public class RamData
{
    public string Name { get; set; } = "DDR";
    public float Used { get; set; }
    public float Total { get; set; }
    public float Percent { get; set; }
}

public class SystemInfoData
{
    public HardwareData Cpu { get; set; } = new();
    public HardwareData Gpu { get; set; } = new();
    public RamData Ram { get; set; } = new();
}

public class SystemInfoService : IDisposable
{
    private Computer? _computer;
    private string _cpuName = "CPU";
    private string _gpuName = "GPU";
    private string _ramName = "DDR";
    private bool _lhmAvailable = false;

    public SystemInfoService()
    {
        LoadHardwareNames();
        InitializeLHM();
    }

    private void InitializeLHM()
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true
            };
            _computer.Open();
            _lhmAvailable = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize LibreHardwareMonitor: {ex.Message}");
            _lhmAvailable = false;
        }
    }

    private void LoadHardwareNames()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                _cpuName = CleanCpuName(obj["Name"]?.ToString() ?? "CPU");
                break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WMI CPU query failed: {ex.Message}");
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                _gpuName = CleanGpuName(obj["Name"]?.ToString() ?? "GPU");
                break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WMI GPU query failed: {ex.Message}");
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Speed FROM Win32_PhysicalMemory");
            string? manufacturer = null;
            uint speed = 0;
            foreach (var obj in searcher.Get())
            {
                manufacturer ??= obj["Manufacturer"]?.ToString()?.Trim();
                var s = obj["Speed"];
                if (s != null) speed = Math.Max(speed, Convert.ToUInt32(s));
            }
            var cleanManufacturer = CleanRamManufacturer(manufacturer);
            _ramName = $"{cleanManufacturer} {speed}MHz";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WMI Memory query failed: {ex.Message}");
        }
    }

    private static string CleanCpuName(string name)
    {
        // Extract just the model number like "i5-14600KF" or "Ryzen 5 5600X"
        name = name.Replace("(R)", "").Replace("(TM)", "").Replace("CPU", "")
                   .Replace("Processor", "").Replace("  ", " ").Trim();
        
        // For Intel: extract i3/i5/i7/i9-XXXXX
        var intelMatch = System.Text.RegularExpressions.Regex.Match(name, @"(i[3579]-\d{4,5}\w*)");
        if (intelMatch.Success) return intelMatch.Value;
        
        // For AMD: extract Ryzen X XXXX
        var amdMatch = System.Text.RegularExpressions.Regex.Match(name, @"(Ryzen\s+\d+\s+\d{4}\w*)");
        if (amdMatch.Success) return amdMatch.Value;
        
        // Fallback: return cleaned name but shorter
        var parts = name.Split(' ');
        if (parts.Length > 3)
            return string.Join(" ", parts.Skip(parts.Length - 2));
        
        return name;
    }

    private static string CleanGpuName(string name) => name
        .Replace("NVIDIA ", "").Replace("AMD ", "").Replace("Intel ", "")
        .Replace("(R)", "").Replace("Graphics", "").Replace("  ", " ").Trim();
    
    private static string CleanRamManufacturer(string? manufacturer)
    {
        if (string.IsNullOrEmpty(manufacturer)) return "DDR";
        
        // Clean up common manufacturer names
        return manufacturer
            .Replace("Technology", "")
            .Replace("Corporation", "")
            .Replace("Inc.", "")
            .Replace("Ltd.", "")
            .Replace("  ", " ")
            .Trim();
    }

    public SystemInfoData GetInfo()
    {
        var data = new SystemInfoData
        {
            Cpu = { Name = _cpuName },
            Gpu = { Name = _gpuName },
            Ram = { Name = _ramName }
        };

        if (_lhmAvailable && _computer != null)
        {
            try
            {
                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();

                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Load && sensor.Name == "CPU Total")
                                data.Cpu.Load = sensor.Value ?? 0;
                            else if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Package"))
                                data.Cpu.Temperature = sensor.Value;
                            else if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core") && sensor.Value > 100)
                                data.Cpu.Speed = Math.Max(data.Cpu.Speed ?? 0, (sensor.Value ?? 0) / 1000f);
                        }
                    }
                    else if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Load && sensor.Name == "GPU Core")
                                data.Gpu.Load = sensor.Value ?? 0;
                            else if (sensor.SensorType == SensorType.Temperature && sensor.Name == "GPU Core")
                                data.Gpu.Temperature = sensor.Value;
                            else if (sensor.SensorType == SensorType.SmallData && sensor.Name == "GPU Memory Used")
                                data.Gpu.Speed = sensor.Value / 1024f;
                        }
                    }
                    else if (hardware.HardwareType == HardwareType.Memory)
                    {
                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Used")
                                data.Ram.Used = sensor.Value ?? 0;
                            else if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Available")
                                data.Ram.Total = data.Ram.Used + (sensor.Value ?? 0);
                            else if (sensor.SensorType == SensorType.Load && sensor.Name == "Memory")
                                data.Ram.Percent = sensor.Value ?? 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating LHM hardware sensors: {ex.Message}");
            }
        }

        // Fallback: get RAM from performance counter
        if (data.Ram.Total == 0)
        {
            try
            {
                var memInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
                data.Ram.Total = memInfo.TotalPhysicalMemory / (1024f * 1024 * 1024);
                data.Ram.Used = data.Ram.Total - (memInfo.AvailablePhysicalMemory / (1024f * 1024 * 1024));
                data.Ram.Percent = (data.Ram.Used / data.Ram.Total) * 100;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RAM ComputerInfo fallback failed: {ex.Message}");
            }
        }

        return data;
    }

    public void Dispose()
    {
        try { _computer?.Close(); }
        catch (Exception ex) { Debug.WriteLine($"Error closing computer sensors: {ex.Message}"); }
    }
}
