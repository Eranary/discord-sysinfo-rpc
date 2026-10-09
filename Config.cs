using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace DiscordSysInfoRPC;

public class Config
{
    public string ClientId { get; set; } = "";
    public string ActivityName { get; set; } = "";
    public int ActivityType { get; set; } = 0;
    public string ImageSet { get; set; } = "kiara";
    public string TimestampMode { get; set; } = "elapsed";
    public string WatchingTimerMode { get; set; } = "progress";
    public int WatchingDurationMinutes { get; set; } = 60;
    public int UpdateInterval { get; set; } = 10000;
    public bool AutoConnect { get; set; } = false;
    public string UiLanguage { get; set; } = "ru";

    public bool DetectForegroundApp { get; set; } = false;
    public bool UseShortcutName { get; set; } = false;
    public bool CustomStatusText { get; set; } = false;
    public string CustomDetails { get; set; } = "";
    public string CustomState { get; set; } = "";
    public bool ShowPromoButton { get; set; } = true;
    public bool ShowButton { get; set; } = false;
    public string ButtonLabel { get; set; } = "Перейти";
    public string ButtonUrl { get; set; } = "";

    public string CustomImageIdle { get; set; } = "";
    public string CustomImageLow { get; set; } = "";
    public string CustomImageMedium { get; set; } = "";
    public string CustomImageHigh { get; set; } = "";
    public int ThresholdLow { get; set; } = 35;
    public int ThresholdMedium { get; set; } = 65;
    public int ThresholdHigh { get; set; } = 75;

    private static string? _resolvedConfigPath;

    public static string ConfigPath
    {
        get
        {
            if (_resolvedConfigPath != null) return _resolvedConfigPath;

            try
            {
                var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
                if (!string.IsNullOrEmpty(exeDir))
                {
                    var localPath = Path.Combine(exeDir, "config.json");
                    if (File.Exists(localPath))
                    {
                        try
                        {
                            using (var fs = File.Open(localPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                            {
                            }
                            _resolvedConfigPath = localPath;
                            return _resolvedConfigPath;
                        }
                        catch
                        {
                            // Local config exists but is not writable, fall through to AppData
                        }
                    }

                    // Test write permission in exe directory
                    var testFile = Path.Combine(exeDir, $".perm_{Guid.NewGuid():N}.tmp");
                    try
                    {
                        File.WriteAllText(testFile, "");
                        File.Delete(testFile);
                        _resolvedConfigPath = localPath;
                        return _resolvedConfigPath;
                    }
                    catch
                    {
                        // Directory is read-only, fall through to AppData
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking local config path: {ex.Message}");
            }

            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DiscordSysInfoRPC"
            );
            try { Directory.CreateDirectory(appDataDir); } catch { }
            _resolvedConfigPath = Path.Combine(appDataDir, "config.json");
            return _resolvedConfigPath;
        }
    }

    public static Config Load()
    {
        try
        {
            // 1. Check primary resolved path
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonConvert.DeserializeObject<Config>(json);
                if (config != null) return config;
            }

            // 2. Check AppData fallback
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DiscordSysInfoRPC",
                "config.json"
            );
            if (File.Exists(appDataPath))
            {
                var json = File.ReadAllText(appDataPath);
                var config = JsonConvert.DeserializeObject<Config>(json);
                if (config != null) return config;
            }

            // 3. Check next to exe if not checked yet
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrEmpty(exeDir))
            {
                var localPath = Path.Combine(exeDir, "config.json");
                if (File.Exists(localPath))
                {
                    var json = File.ReadAllText(localPath);
                    var config = JsonConvert.DeserializeObject<Config>(json);
                    if (config != null) return config;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load config: {ex.Message}");
        }

        return new Config();
    }

    public void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save config to {ConfigPath}: {ex.Message}");
            try
            {
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DiscordSysInfoRPC"
                );
                Directory.CreateDirectory(appDataDir);
                var fallbackPath = Path.Combine(appDataDir, "config.json");
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(fallbackPath, json);
                _resolvedConfigPath = fallbackPath;
            }
            catch (Exception ex2)
            {
                Debug.WriteLine($"Failed to save fallback config: {ex2.Message}");
            }
        }
    }
}
