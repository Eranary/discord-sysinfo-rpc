using System.Diagnostics;
using LibreHardwareMonitor.Hardware;
using System.Management;
using System.Text.RegularExpressions;

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
        // 1. CPU Name
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                var rawName = obj["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(rawName))
                {
                    _cpuName = CleanCpuName(rawName);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WMI CPU query failed: {ex.Message}");
        }

        // 2. GPU Name (Pick discrete GPU on dual-GPU laptops)
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            var gpus = new List<(string Name, int Priority)>();
            foreach (var obj in searcher.Get())
            {
                var rawName = obj["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(rawName)) continue;

                var priority = ScoreGpu(rawName);
                gpus.Add((rawName, priority));
            }

            if (gpus.Count > 0)
            {
                var best = gpus.OrderByDescending(g => g.Priority).First();
                _gpuName = CleanGpuName(best.Name);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WMI GPU query failed: {ex.Message}");
        }

        // 3. RAM Name (Clean manufacturer, avoid 0000 / dummy IDs, fallback to DDR3/4/5)
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, PartNumber, Speed, ConfiguredClockSpeed, SMBIOSMemoryType FROM Win32_PhysicalMemory");
            string? validManufacturer = null;
            string? partNumber = null;
            uint speed = 0;
            uint smbiosType = 0;

            foreach (var obj in searcher.Get())
            {
                var m = obj["Manufacturer"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(m) && IsValidRamManufacturer(m))
                    validManufacturer ??= m;

                var pn = obj["PartNumber"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(pn))
                    partNumber ??= pn;

                var s = obj["ConfiguredClockSpeed"] ?? obj["Speed"];
                if (s != null)
                {
                    try
                    {
                        var sp = Convert.ToUInt32(s);
                        if (sp > speed) speed = sp;
                    }
                    catch { }
                }

                var mt = obj["SMBIOSMemoryType"];
                if (mt != null && smbiosType == 0)
                {
                    try { smbiosType = Convert.ToUInt32(mt); } catch { }
                }
            }

            var brand = !string.IsNullOrEmpty(validManufacturer)
                ? CleanRamManufacturer(validManufacturer)
                : ExtractBrandFromPartNumber(partNumber);

            var ddrGen = GetDdrGeneration(smbiosType, speed);

            if (!string.IsNullOrEmpty(brand))
            {
                if (brand.Contains("DDR", StringComparison.OrdinalIgnoreCase))
                    _ramName = speed > 0 ? $"{brand} {speed}MHz" : brand;
                else
                    _ramName = speed > 0 ? $"{brand} {speed}MHz" : $"{brand} {ddrGen}";
            }
            else
            {
                _ramName = speed > 0 ? $"{ddrGen} {speed}MHz" : ddrGen;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WMI Memory query failed: {ex.Message}");
        }
    }

    private static string CleanCpuName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "CPU";

        name = name.Replace("(R)", "").Replace("(TM)", "").Replace("(tm)", "").Trim();

        // Remove frequency suffix like "@ 2.30GHz", "@ 3.4GHz", "@ 2300MHz"
        name = Regex.Replace(name, @"\s*@\s*[\d\.,]+\s*[A-Za-z]+.*$", "", RegexOptions.IgnoreCase).Trim();

        // Remove core count suffix like "8-Core Processor", "Eight-Core Processor", "Quad-Core", etc.
        name = Regex.Replace(name, @"\s+(?:\d+|Eight|Six|Quad|Dual)-Core.*$", "", RegexOptions.IgnoreCase).Trim();

        // Remove "with Radeon ... Graphics"
        name = Regex.Replace(name, @"\s+with\s+Radeon.*$", "", RegexOptions.IgnoreCase).Trim();

        // Remove redundant words
        name = name.Replace("CPU", "").Replace("Processor", "").Replace("Genuine", "").Replace("AuthenticAMD", "").Trim();
        name = Regex.Replace(name, @"\s+", " ");

        // 1. Intel Xeon: e.g. "Xeon E5-2670 v3", "Xeon Gold 6248R", "Xeon E3-1270 v3", "Xeon X5670", "Xeon W-2145"
        var xeonMatch = Regex.Match(name, @"(Xeon(?:\s+(?:Bronze|Silver|Gold|Platinum))?(?:\s+[A-Za-z\d\-]+)+(?:\s+v\d+)?)", RegexOptions.IgnoreCase);
        if (xeonMatch.Success) return xeonMatch.Value.Trim();

        // 2. Intel Core Ultra: e.g. "Core Ultra 7 155H"
        var ultraMatch = Regex.Match(name, @"(Core\s+Ultra\s+\d+\s+\w+)", RegexOptions.IgnoreCase);
        if (ultraMatch.Success) return ultraMatch.Value.Trim();

        // 3. Intel Core i-series: e.g. "i5-14600KF", "i7-13700K", "i3-10100"
        var intelMatch = Regex.Match(name, @"(i[3579][\-\s]\d{3,5}\w*)", RegexOptions.IgnoreCase);
        if (intelMatch.Success) return intelMatch.Value.Trim().Replace(" ", "-");

        // 4. Intel Core 2 Quad / Duo: e.g. "Core 2 Quad Q9550", "Core 2 Duo E8400"
        var core2Match = Regex.Match(name, @"(Core\s*2\s*(?:Quad|Duo|Extreme)\s+[A-Za-z\d]+)", RegexOptions.IgnoreCase);
        if (core2Match.Success) return core2Match.Value.Trim();

        // 5. Intel Pentium / Celeron: e.g. "Pentium Gold G6400", "Celeron G5905"
        var pentiumMatch = Regex.Match(name, @"((?:Pentium|Celeron)(?:\s+(?:Gold|Silver|Dual-Core))?\s+[A-Za-z\d]+)", RegexOptions.IgnoreCase);
        if (pentiumMatch.Success) return pentiumMatch.Value.Trim();

        // 6. AMD Ryzen / Threadripper: e.g. "Ryzen 7 7800X3D", "Ryzen 5 5600X", "Ryzen Threadripper 3990X"
        var ryzenMatch = Regex.Match(name, @"(Ryzen(?:\s+Threadripper)?(?:\s+PRO)?\s+\d+\s+\w+(?:\s+X3D|\s+PRO)?)", RegexOptions.IgnoreCase);
        if (ryzenMatch.Success) return ryzenMatch.Value.Trim();

        var ryzenSimple = Regex.Match(name, @"(Ryzen\s+\d+\s+\w+)", RegexOptions.IgnoreCase);
        if (ryzenSimple.Success) return ryzenSimple.Value.Trim();

        // 7. AMD EPYC
        var epycMatch = Regex.Match(name, @"(EPYC\s+[A-Za-z\d]+)", RegexOptions.IgnoreCase);
        if (epycMatch.Success) return epycMatch.Value.Trim();

        // 8. AMD FX / Phenom / Athlon: e.g. "FX-8350", "Athlon 3000G", "Phenom II X6 1090T"
        var legacyAmdMatch = Regex.Match(name, @"((?:FX|Phenom(?:\s+II)?(?:\s+X\d+)?|Athlon(?:\s+(?:Gold|Silver|X4|X2))?)\s*[\-\s]?\w+)", RegexOptions.IgnoreCase);
        if (legacyAmdMatch.Success) return legacyAmdMatch.Value.Trim();

        // Fallback: strip "Intel", "AMD" and extra whitespace
        name = name.Replace("Intel", "").Replace("AMD", "").Trim();
        name = Regex.Replace(name, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(name) ? "CPU" : name;
    }

    private static int ScoreGpu(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("rtx") || lower.Contains("gtx") || lower.Contains("geforce") || lower.Contains("quadro") || lower.Contains("titan") || lower.Contains("tesla"))
            return 100;
        if (lower.Contains("radeon rx") || lower.Contains("radeon pro") || Regex.IsMatch(lower, @"radeon\s+hd\s+\d{4}"))
            return 90;
        if (lower.Contains("arc") || lower.Contains("intel arc"))
            return 85;
        if (lower.Contains("radeon"))
            return 50;
        if (lower.Contains("intel") || lower.Contains("iris") || lower.Contains("hd graphics") || lower.Contains("uhd graphics"))
            return 40;
        if (lower.Contains("basic") || lower.Contains("microsoft") || lower.Contains("virtual") || lower.Contains("remote"))
            return 10;
        return 30;
    }

    private static string CleanGpuName(string name)
    {
        name = name.Replace("(R)", "").Replace("(TM)", "").Replace("(tm)", "").Replace("  ", " ").Trim();
        name = name.Replace("Graphics Adapter", "").Replace("Display Adapter", "");
        name = name.Replace("Graphics", "").Replace("  ", " ").Trim();

        if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            return name.Replace("Corporation", "").Trim();
        }

        name = name.Replace("NVIDIA ", "");
        name = name.Replace("AMD ", "");
        return name.Trim();
    }

    private static bool IsValidRamManufacturer(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        name = name.Trim();
        if (name.Length < 2) return false;

        var lower = name.ToLowerInvariant();
        if (lower is "unknown" or "undefined" or "none" or "n/a" or "system" or "generic" or "default" or "not specified" or "no dimm" or "empty")
            return false;
        if (lower.StartsWith("manufacturer") || lower.StartsWith("array") || lower.StartsWith("serail") || lower.StartsWith("module") || lower.StartsWith("channel") || lower.StartsWith("dimm") || lower.StartsWith("slot"))
            return false;

        // Check for dummy hex strings like "0000", "0x0000", "FFFF", "04CB"
        var cleanHex = lower.StartsWith("0x") ? lower.Substring(2) : lower;
        if (cleanHex.All(c => c == '0' || c == 'f')) return false;
        if (cleanHex.Length <= 4 && cleanHex.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
            return false;

        return true;
    }

    private static string CleanRamManufacturer(string? manufacturer)
    {
        if (string.IsNullOrWhiteSpace(manufacturer)) return "";

        return manufacturer
            .Replace("Technology", "")
            .Replace("Corporation", "")
            .Replace("Electronics", "")
            .Replace("Semiconductor", "")
            .Replace("Memory", "")
            .Replace("Inc.", "")
            .Replace("Ltd.", "")
            .Replace("Co.", "")
            .Replace("  ", " ")
            .Trim();
    }

    private static string? ExtractBrandFromPartNumber(string? partNumber)
    {
        if (string.IsNullOrWhiteSpace(partNumber)) return null;
        var pn = partNumber.Trim().ToUpperInvariant();

        if (pn.Contains("SAMSUNG") || pn.StartsWith("M378") || pn.StartsWith("M393") || pn.StartsWith("SEC"))
            return "Samsung";
        if (pn.Contains("HYNIX") || pn.StartsWith("HMT") || pn.StartsWith("HMA") || pn.StartsWith("HMC"))
            return "Hynix";
        if (pn.Contains("MICRON") || pn.StartsWith("MTA") || pn.StartsWith("MT8") || pn.StartsWith("MT16"))
            return "Micron";
        if (pn.Contains("CRUCIAL") || pn.StartsWith("CT"))
            return "Crucial";
        if (pn.Contains("KINGSTON") || pn.Contains("HYPERX") || pn.Contains("FURY") || pn.StartsWith("KVR") || pn.StartsWith("KF"))
            return "Kingston";
        if (pn.Contains("CORSAIR") || pn.StartsWith("CMK") || pn.StartsWith("CMW") || pn.StartsWith("CMT"))
            return "Corsair";
        if (pn.Contains("G.SKILL") || pn.StartsWith("F4-") || pn.StartsWith("F5-"))
            return "G.Skill";
        if (pn.Contains("ADATA") || pn.Contains("XPG"))
            return "ADATA";
        if (pn.Contains("KLLISRE")) return "Kllisre";
        if (pn.Contains("ATERMITER")) return "Atermiter";
        if (pn.Contains("MACHINIST")) return "Machinist";
        if (pn.Contains("JUHOR")) return "Juhor";
        if (pn.Contains("KINGBANK")) return "Kingbank";
        if (pn.Contains("GLOWAY")) return "Gloway";
        if (pn.Contains("ASGARD")) return "Asgard";
        if (pn.Contains("NETAC")) return "Netac";

        return null;
    }

    private static string GetDdrGeneration(uint smbiosType, uint speed)
    {
        return smbiosType switch
        {
            20 => "DDR",
            21 => "DDR2",
            24 => "DDR3",
            26 => "DDR4",
            30 => "LPDDR4",
            34 => "DDR5",
            35 => "LPDDR5",
            _ => speed switch
            {
                <= 800 and > 0 => "DDR2",
                > 800 and <= 1866 => "DDR3",
                >= 2133 and <= 4266 => "DDR4",
                >= 4800 => "DDR5",
                _ => "DDR"
            }
        };
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
                var gpus = new List<IHardware>();

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

                        // Fallback for CPU temperature if "Package" was not found (e.g. AMD Tctl/Tdie or "Core #1")
                        if (data.Cpu.Temperature == null)
                        {
                            foreach (var sensor in hardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature && (sensor.Name.Contains("Tctl") || sensor.Name.Contains("Core") || sensor.Name.Contains("CPU")))
                                {
                                    data.Cpu.Temperature = sensor.Value;
                                    break;
                                }
                            }
                        }
                    }
                    else if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                    {
                        gpus.Add(hardware);
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

                // Read sensors from the chosen GPU
                if (gpus.Count > 0)
                {
                    var targetGpu = gpus.FirstOrDefault(g => g.HardwareType == HardwareType.GpuNvidia)
                                 ?? gpus.FirstOrDefault(g => g.HardwareType == HardwareType.GpuAmd)
                                 ?? gpus.FirstOrDefault(g => g.HardwareType == HardwareType.GpuIntel)
                                 ?? gpus[0];

                    foreach (var sensor in targetGpu.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load)
                        {
                            // On Intel iGPU, load is "D3D 3D", "GPU D3D 3D", "D3D", "Graphics"
                            // On Nvidia/AMD, load is "GPU Core", "GPU 3D"
                            if (sensor.Name is "GPU Core" or "D3D 3D" or "GPU D3D 3D" or "D3D" or "Graphics")
                            {
                                data.Gpu.Load = Math.Max(data.Gpu.Load, sensor.Value ?? 0);
                            }
                            else if (data.Gpu.Load == 0 && (sensor.Name.Contains("Core") || sensor.Name.Contains("3D") || sensor.Name.Contains("Total")))
                            {
                                data.Gpu.Load = Math.Max(data.Gpu.Load, sensor.Value ?? 0);
                            }
                        }
                        else if (sensor.SensorType == SensorType.Temperature)
                        {
                            if (sensor.Name is "GPU Core" or "GPU Temperature" or "Temperature" or "Core")
                            {
                                data.Gpu.Temperature ??= sensor.Value;
                            }
                            else if (data.Gpu.Temperature == null && (sensor.Name.Contains("Core") || sensor.Name.Contains("Hot Spot") || sensor.Name.Contains("GPU")))
                            {
                                data.Gpu.Temperature = sensor.Value;
                            }
                        }
                        else if (sensor.Name is "GPU Memory Used" or "D3D Shared Memory Used" or "D3D Memory Used" or "Memory Used")
                        {
                            if (sensor.SensorType == SensorType.SmallData)
                            {
                                var val = (sensor.Value ?? 0) / 1024f;
                                if (val > 0.05f) data.Gpu.Speed = val;
                            }
                            else if (sensor.SensorType == SensorType.Data)
                            {
                                var val = sensor.Value ?? 0;
                                var gb = val > 100 ? val / 1024f : val;
                                if (gb > 0.05f) data.Gpu.Speed = gb;
                            }
                        }
                    }

                    // Fallback for integrated GPU temperature: on Intel HD/UHD/Iris or AMD Vega,
                    // GPU is on the CPU die so CPU temperature is accurate
                    if (!data.Gpu.Temperature.HasValue && data.Cpu.Temperature.HasValue)
                    {
                        if (_gpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                            _gpuName.Contains("Vega", StringComparison.OrdinalIgnoreCase) ||
                            _gpuName.Contains("HD ", StringComparison.OrdinalIgnoreCase) ||
                            _gpuName.Contains("UHD", StringComparison.OrdinalIgnoreCase) ||
                            _gpuName.Contains("Iris", StringComparison.OrdinalIgnoreCase) ||
                            _gpuName.Contains("Graphics", StringComparison.OrdinalIgnoreCase))
                        {
                            data.Gpu.Temperature = data.Cpu.Temperature;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating LHM hardware sensors: {ex.Message}");
            }
        }

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
