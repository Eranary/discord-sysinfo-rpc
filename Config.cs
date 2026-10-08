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

    private static readonly string ConfigPath = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? ".",
        "config.json"
    );

    public static Config Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load config: {ex.Message}");
        }
        
        var config = new Config();
        config.Save();
        return config;
    }

    public void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }
}
