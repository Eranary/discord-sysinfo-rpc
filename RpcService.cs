using System.Diagnostics;
using System.Timers;

namespace DiscordSysInfoRPC;

public class ImageSet
{
    public string Name { get; set; } = "";
    public string Idle { get; set; } = "";
    public string Low { get; set; } = "";
    public string Medium { get; set; } = "";
    public string High { get; set; } = "";
}

public class RpcService : IDisposable
{
    private DiscordIpcClient? _client;
    private Config _config;
    private readonly SystemInfoService _systemInfo;
    private System.Timers.Timer? _updateTimer;
    private DateTime _startTime;
    private int _displayMode;
    private DateTime _lastModeChange;
    private string? _detectedExePath;
    private string? _detectedIconUrl;
    private int _iconResolveBusy;

    public event Action<bool>? OnConnectionChanged;

    private int _showingError;

    private static readonly Dictionary<string, ImageSet> ImageSets = new()
    {
        ["kiara"] = new ImageSet
        {
            Name = "Kiara",
            Idle = "https://media.tenor.com/3xXNfoL9hmwAAAAi/smol-kiara-hololive-en.gif",
            Low = "https://media.tenor.com/1Q814osMSxMAAAAi/smol-kiara-hololive-en.gif",
            Medium = "https://media.tenor.com/PO6d_uSTa_sAAAAi/smol-kiara-hololive-en.gif",
            High = "https://media.tenor.com/PO6d_uSTa_sAAAAi/smol-kiara-hololive-en.gif"
        },
        ["gura"] = new ImageSet
        {
            Name = "Gura",
            Idle = "https://media.tenor.com/chN30h1tSHsAAAAj/gawr-gura-smol-gura.gif",
            Low = "https://media.tenor.com/rBpFtUBdafkAAAAj/gura.gif",
            Medium = "https://media.tenor.com/tFrwPTMtwUgAAAAj/guradrum-gurataiko.gif",
            High = "https://media.tenor.com/rgIeiFVZJG0AAAAi/gawr-gura-smol-gura.gif"
        },
        ["mori"] = new ImageSet
        {
            Name = "Mori",
            Idle = "https://media.tenor.com/nnvxtjpyquMAAAAi/mori-calliope-hololive.gif",
            Low = "https://media.tenor.com/Q-dCp6lakPIAAAAi/mori-calliope-hololive.gif",
            Medium = "https://media.tenor.com/HtBpq8rfCn0AAAAi/mori-calliope-hololive.gif",
            High = "https://media.tenor.com/Uv_G-mhR-poAAAAi/mori-calliope-hololive.gif"
        },
        ["discord_label"] = new ImageSet
        {
            Name = "Discord Label",
            Idle = "https://cdn.discordapp.com/emojis/1375250761553809419.webp?size=128&animated=true",
            Low = "https://cdn.discordapp.com/emojis/1375250761553809419.webp?size=128&animated=true",
            Medium = "https://cdn.discordapp.com/emojis/1375250761553809419.webp?size=128&animated=true",
            High = "https://cdn.discordapp.com/emojis/1375250761553809419.webp?size=128&animated=true"
        }
    };

    private const string FallbackImageUrl = "https://cdn.discordapp.com/emojis/1375250761553809419.webp?size=128&animated=true";

    public bool IsConnected => _client?.IsConnected == true;

    public RpcService(Config config, SystemInfoService systemInfo)
    {
        _config = config;
        _systemInfo = systemInfo;
    }

    public async void Connect()
    {
        if (_client != null) Disconnect();

        try
        {
            if (string.IsNullOrWhiteSpace(_config.ClientId))
            {
                ShowError("Application ID не указан!");
                OnConnectionChanged?.Invoke(false);
                return;
            }

            _client = new DiscordIpcClient();
            
            _client.OnReady += () =>
            {
                try
                {
                    _startTime = DateTime.UtcNow;
                    _displayMode = 0;
                    _lastModeChange = DateTime.UtcNow;

                    StartUpdateTimer();
                    OnConnectionChanged?.Invoke(true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnReady handler error: {ex.Message}");
                    OnConnectionChanged?.Invoke(false);
                }
            };

            _client.OnError += (msg) =>
            {
                Debug.WriteLine($"Discord IPC error: {msg}");
                Disconnect();
                ShowError(msg);
            };

            _client.OnDisconnected += () =>
            {
                OnConnectionChanged?.Invoke(false);
            };

            var connected = await _client.ConnectAsync(_config.ClientId);
            
            if (!connected)
            {
                OnConnectionChanged?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка: {ex.Message}");
            OnConnectionChanged?.Invoke(false);
        }
    }

    private void ShowError(string message)
    {
        if (Interlocked.CompareExchange(ref _showingError, 1, 0) != 0) return;
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            try
            {
                System.Windows.MessageBox.Show(message, "Discord RPC", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally
            {
                Interlocked.Exchange(ref _showingError, 0);
            }
        });
    }

    private void StartUpdateTimer()
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        
        _updateTimer = new System.Timers.Timer(_config.UpdateInterval);
        _updateTimer.Elapsed += (s, e) => UpdateActivity();
        _updateTimer.AutoReset = true;
        _updateTimer.Start();
        
        // Initial update
        UpdateActivity();
    }

    public void Disconnect()
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _updateTimer = null;

        if (_client != null)
        {
            try { _client.ClearActivityAsync().Wait(1000); }
            catch (Exception ex) { Debug.WriteLine($"ClearActivity error: {ex.Message}"); }

            try { _client.Disconnect(); }
            catch (Exception ex) { Debug.WriteLine($"Disconnect error: {ex.Message}"); }

            try { _client.Dispose(); }
            catch (Exception ex) { Debug.WriteLine($"Dispose client error: {ex.Message}"); }

            _client = null;
        }

        OnConnectionChanged?.Invoke(false);
    }

    public void UpdateConfig(Config config)
    {
        if (config.ActivityType == 3 && _config.ActivityType != 3)
            _startTime = DateTime.UtcNow;
        if (config.ActivityType == 3 && config.WatchingTimerMode == "progress" && _config.WatchingTimerMode != "progress")
            _startTime = DateTime.UtcNow;
        if (config.ActivityType == 3 && config.WatchingDurationMinutes != _config.WatchingDurationMinutes)
            _startTime = DateTime.UtcNow;
        _config = config;
        if (_updateTimer != null)
            _updateTimer.Interval = config.UpdateInterval;
        if (IsConnected)
            UpdateActivity();
    }

    private async void UpdateActivity()
    {
        if (_client == null || !_client.IsConnected) return;

        try
        {
            var info = _systemInfo.GetInfo();

            // Mode rotation
            if ((DateTime.UtcNow - _lastModeChange).TotalMilliseconds >= _config.UpdateInterval)
            {
                _displayMode = (_displayMode + 1) % 3;
                _lastModeChange = DateTime.UtcNow;
            }

            string? details = null;
            string? state = null;

            if (_config.CustomStatusText)
            {
                details = FormatCustomText(_config.CustomDetails, info);
                state = FormatCustomText(_config.CustomState, info);
            }
            else
            {
                switch (_displayMode)
                {
                    case 0:
                        details = GenerateLoadBar(info.Cpu.Load);
                        var cpuParts = new List<string> { info.Cpu.Name };
                        if (info.Cpu.Temperature.HasValue) cpuParts.Add($"{info.Cpu.Temperature:F0}°C");
                        if (info.Cpu.Speed.HasValue) cpuParts.Add($"{info.Cpu.Speed:F2} GHz");
                        state = string.Join(" | ", cpuParts);
                        break;

                    case 1:
                        details = GenerateLoadBar(info.Gpu.Load);
                        var gpuParts = new List<string> { info.Gpu.Name };
                        if (info.Gpu.Temperature.HasValue) gpuParts.Add($"{info.Gpu.Temperature:F0}°C");
                        if (info.Gpu.Speed.HasValue) gpuParts.Add($"{info.Gpu.Speed:F1} GB");
                        state = string.Join(" | ", gpuParts);
                        break;

                    default:
                        details = GenerateLoadBar(info.Ram.Percent);
                        state = $"{info.Ram.Name} | {info.Ram.Used:F1} / {info.Ram.Total:F1} GB";
                        break;
                }
            }

            details = DiscordLimits.SanitizeText(details);
            state = DiscordLimits.SanitizeText(state);

            var (startTimestamp, endTimestamp) = GetTimestamps();

            var imageUrl = GetImageForLoad(info.Cpu.Load);

            // Activity name from config, or null to use app name from portal
            string? activityName = DiscordLimits.SanitizeText(_config.ActivityName, DiscordLimits.MaxActivityNameBytes);

            if (_config.DetectForegroundApp)
            {
                var detected = ForegroundAppDetector.GetSticky(_config.UseShortcutName);
                if (detected != null)
                {
                    var cleanDetected = DiscordLimits.SanitizeText(detected.Name, DiscordLimits.MaxActivityNameBytes);
                    if (!string.IsNullOrWhiteSpace(cleanDetected))
                        activityName = cleanDetected;

                    if (!string.Equals(_detectedExePath, detected.ExePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _detectedExePath = detected.ExePath;
                        _detectedIconUrl = null;
                    }

                    if (!string.IsNullOrWhiteSpace(_detectedIconUrl))
                    {
                        imageUrl = _detectedIconUrl;
                    }
                    else
                    {
                        TryStartIconResolve(detected.ExePath);
                    }
                }
            }

            var buttons = new List<(string label, string url)>();

            if (_config.ShowButton)
            {
                var validLabel = DiscordLimits.SanitizeButtonLabel(_config.ButtonLabel);
                var validUrl = DiscordLimits.SanitizeButtonUrl(_config.ButtonUrl);
                if (!string.IsNullOrWhiteSpace(validLabel) && !string.IsNullOrWhiteSpace(validUrl))
                {
                    buttons.Add((validLabel, validUrl));
                }
            }

            if (_config.ShowPromoButton)
            {
                string promoLabel = Loc.Get("PromoButtonLabel");
                if (string.IsNullOrWhiteSpace(promoLabel) || promoLabel == "PromoButtonLabel")
                    promoLabel = AppConstants.PromoButtonLabel;
                var validPromoLabel = DiscordLimits.SanitizeButtonLabel(promoLabel);
                var validPromoUrl = DiscordLimits.SanitizeButtonUrl(AppConstants.PromoButtonUrl);
                if (!string.IsNullOrWhiteSpace(validPromoLabel) && !string.IsNullOrWhiteSpace(validPromoUrl))
                    buttons.Add((validPromoLabel, validPromoUrl));
            }

            // CPU under image only for Playing (disabled if CustomStatusText is active)
            string? largeImageText = (_config.ActivityType == 0 && !_config.CustomStatusText)
                ? $"CPU: {info.Cpu.Load:F0}%"
                : null;

            await _client.SetActivityAsync(
                activityName: activityName,
                activityType: _config.ActivityType,
                details: details,
                state: state,
                startTimestamp: startTimestamp,
                endTimestamp: endTimestamp,
                largeImageKey: imageUrl,
                largeImageText: largeImageText,
                buttons: buttons
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UpdateActivity error: {ex.Message}");
        }
    }

    private (DateTime? start, DateTime? end) GetTimestamps()
    {
        // Watching (3): progress bar OR elapsed / start of day
        if (_config.ActivityType == 3)
        {
            if (_config.WatchingTimerMode == "elapsed")
                return (_startTime, null);
            if (_config.WatchingTimerMode == "startOfDay")
                return (DateTime.Today.ToUniversalTime(), null);

            var totalMinutes = Math.Clamp(_config.WatchingDurationMinutes, 1, 1440);
            var duration = TimeSpan.FromMinutes(totalMinutes);
            var elapsed = DateTime.UtcNow - _startTime;
            if (elapsed < TimeSpan.Zero || elapsed >= duration)
                _startTime = DateTime.UtcNow;

            return (_startTime, _startTime.Add(duration));
        }

        // Playing (0), Listening (2), Competing (5): elapsed or start of day
        if (_config.ActivityType is 0 or 2 or 5)
        {
            var start = _config.TimestampMode == "startOfDay"
                ? DateTime.Today.ToUniversalTime()
                : _startTime;
            return (start, null);
        }

        return (null, null);
    }

    private static string GenerateLoadBar(float percentage, int length = 10)
    {
        var filled = (int)Math.Round(percentage / 100f * length);
        filled = Math.Clamp(filled, 0, length);
        return new string('▰', filled) + new string('▱', length - filled);
    }

    private string GetImageForLoad(float percentage)
    {
        string url;

        if (_config.ImageSet == "custom")
            url = ResolveCustomImage(percentage);
        else
            url = GetPresetImage(_config.ImageSet, percentage);

        return string.IsNullOrWhiteSpace(url) ? FallbackImageUrl : url;
    }

    private string GetPresetImage(string setName, float percentage)
    {
        if (!ImageSets.TryGetValue(setName, out var set))
            set = ImageSets["kiara"];

        var thresholdLow = _config.ThresholdLow;
        var thresholdMed = _config.ThresholdMedium;
        var thresholdHigh = _config.ThresholdHigh;

        if (percentage < thresholdLow) return set.Idle;
        if (percentage < thresholdMed) return set.Low;
        if (percentage < thresholdHigh) return set.Medium;
        return set.High;
    }

    /// <summary>
    /// Custom slots: forward-fill last set URL (1 image → all tiers; 2 images → tier 3–4 use 2nd).
    /// </summary>
    private string ResolveCustomImage(float percentage)
    {
        var slots = new[]
        {
            _config.CustomImageIdle,
            _config.CustomImageLow,
            _config.CustomImageMedium,
            _config.CustomImageHigh
        };

        var resolved = new string[4];
        var last = "";
        for (var i = 0; i < 4; i++)
        {
            if (!string.IsNullOrWhiteSpace(slots[i]))
                last = slots[i]!.Trim();
            resolved[i] = last;
        }

        if (string.IsNullOrEmpty(resolved[3]))
            return GetPresetImage("kiara", percentage);

        var thresholdLow = _config.ThresholdLow;
        var thresholdMed = _config.ThresholdMedium;
        var thresholdHigh = _config.ThresholdHigh;

        var tier = percentage < thresholdLow ? 0
            : percentage < thresholdMed ? 1
            : percentage < thresholdHigh ? 2
            : 3;

        return resolved[tier];
    }

    private string? FormatCustomText(string? template, SystemInfoData info)
    {
        if (string.IsNullOrWhiteSpace(template))
            return null;

        var text = template
            .Replace("{bar}", GenerateLoadBar(info.Cpu.Load), StringComparison.OrdinalIgnoreCase)
            .Replace("{cpu_load}", $"{info.Cpu.Load:F0}%", StringComparison.OrdinalIgnoreCase)
            .Replace("{cpu_temp}", info.Cpu.Temperature.HasValue ? $"{info.Cpu.Temperature:F0}°C" : "", StringComparison.OrdinalIgnoreCase)
            .Replace("{cpu_speed}", info.Cpu.Speed.HasValue ? $"{info.Cpu.Speed:F2} GHz" : "", StringComparison.OrdinalIgnoreCase)
            .Replace("{cpu_ghz}", info.Cpu.Speed.HasValue ? $"{info.Cpu.Speed:F2} GHz" : "", StringComparison.OrdinalIgnoreCase)
            .Replace("{cpu_name}", info.Cpu.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{cpu}", info.Cpu.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{gpu_load}", $"{info.Gpu.Load:F0}%", StringComparison.OrdinalIgnoreCase)
            .Replace("{gpu_temp}", info.Gpu.Temperature.HasValue ? $"{info.Gpu.Temperature:F0}°C" : "", StringComparison.OrdinalIgnoreCase)
            .Replace("{gpu_mem}", info.Gpu.Speed.HasValue ? $"{info.Gpu.Speed:F1} GB" : "", StringComparison.OrdinalIgnoreCase)
            .Replace("{gpu_name}", info.Gpu.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{gpu}", info.Gpu.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{ram_percent}", $"{info.Ram.Percent:F0}%", StringComparison.OrdinalIgnoreCase)
            .Replace("{ram_used}", $"{info.Ram.Used:F1} GB", StringComparison.OrdinalIgnoreCase)
            .Replace("{ram_total}", $"{info.Ram.Total:F1} GB", StringComparison.OrdinalIgnoreCase)
            .Replace("{ram_name}", info.Ram.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{ram}", info.Ram.Name, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (string.IsNullOrWhiteSpace(text))
            return null;

        return DiscordLimits.SanitizeText(text, DiscordLimits.MaxDetailsBytes);
    }

    private void TryStartIconResolve(string exePath)
    {
        if (!AppConstants.CanUploadIcons) return;
        if (Interlocked.CompareExchange(ref _iconResolveBusy, 1, 0) != 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var png = ExeIconExtractor.TryGetPng(exePath);
                if (png == null || png.Length == 0) return;

                var url = await IconUploadService.ResolveUrlAsync(png);
                if (!string.IsNullOrWhiteSpace(url)
                    && string.Equals(_detectedExePath, exePath, StringComparison.OrdinalIgnoreCase))
                {
                    _detectedIconUrl = url;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to resolve process icon: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _iconResolveBusy, 0);
            }
        });
    }

    public SystemInfoData GetCurrentInfo() => _systemInfo.GetInfo();

    public void Dispose()
    {
        Disconnect();
    }
}
