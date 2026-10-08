using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordSysInfoRPC;

public class DiscordIpcClient : IDisposable
{
    private NamedPipeClientStream? _pipe;
    private bool _isConnected;
    private string _clientId = "";
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    public event Action? OnReady;
    public event Action<string>? OnError;
    public event Action? OnDisconnected;

    public bool IsConnected => _isConnected;
    public string? DiscordUserId { get; private set; }
    public string? DiscordUsername { get; private set; }

    public async Task<bool> ConnectAsync(string clientId)
    {
        _clientId = clientId;

        // Try pipes 0-9
        for (int i = 0; i < 10; i++)
        {
            var pipeName = $"discord-ipc-{i}";
            try
            {
                _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                
                await _pipe.ConnectAsync(1000);
                
                if (_pipe.IsConnected)
                {
                    // Send handshake
                    var handshake = new { v = 1, client_id = clientId };
                    await SendFrameAsync(0, handshake); // Opcode 0 = HANDSHAKE

                    // Start reading
                    _readCts = new CancellationTokenSource();
                    _readTask = ReadLoopAsync(_readCts.Token);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to connect to pipe {pipeName}: {ex.Message}");
                _pipe?.Dispose();
                _pipe = null;
            }
        }

        OnError?.Invoke("Не удалось подключиться к Discord. Убедитесь что Discord запущен.");
        return false;
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var headerBuffer = new byte[8];

        try
        {
            while (!ct.IsCancellationRequested && _pipe != null && _pipe.IsConnected)
            {
                // Read header (8 bytes: 4 opcode + 4 length)
                var bytesRead = await _pipe.ReadAsync(headerBuffer, 0, 8, ct);
                if (bytesRead < 8) break;

                var opcode = BitConverter.ToInt32(headerBuffer, 0);
                var length = BitConverter.ToInt32(headerBuffer, 4);

                if (length > 0)
                {
                    var dataBuffer = new byte[length];
                    bytesRead = await _pipe.ReadAsync(dataBuffer, 0, length, ct);
                    if (bytesRead < length) break;

                    var json = Encoding.UTF8.GetString(dataBuffer);
                    HandleMessage(opcode, json);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on disconnect
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IPC read loop error: {ex.Message}");
        }

        _isConnected = false;
        OnDisconnected?.Invoke();
    }

    private void HandleMessage(int opcode, string json)
    {
        try
        {
            var data = JObject.Parse(json);
            var cmd = data["cmd"]?.ToString();
            var evt = data["evt"]?.ToString();

            if (cmd == "DISPATCH" && evt == "READY")
            {
                var user = data["data"]?["user"];
                DiscordUserId = user?["id"]?.ToString();
                var globalName = user?["global_name"]?.ToString();
                var username = user?["username"]?.ToString();
                DiscordUsername = string.IsNullOrWhiteSpace(globalName) ? username : globalName;
                _isConnected = true;
                OnReady?.Invoke();
            }
            else if (evt == "ERROR")
            {
                var msg = data["data"]?["message"]?.ToString() ?? "Unknown error";
                OnError?.Invoke(msg);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error handling IPC message: {ex.Message}");
        }
    }

    public Task SetActivityAsync(string? activityName, int activityType, string? details, string? state, 
        DateTime? startTimestamp, DateTime? endTimestamp, string? largeImageKey, string? largeImageText,
        string? buttonLabel, string? buttonUrl)
    {
        var list = new List<(string, string)>();
        if (!string.IsNullOrEmpty(buttonLabel) && !string.IsNullOrEmpty(buttonUrl))
            list.Add((buttonLabel, buttonUrl));
        return SetActivityAsync(activityName, activityType, details, state, startTimestamp, endTimestamp,
            largeImageKey, largeImageText, list);
    }

    public async Task SetActivityAsync(string? activityName, int activityType, string? details, string? state, 
        DateTime? startTimestamp, DateTime? endTimestamp, string? largeImageKey, string? largeImageText,
        IReadOnlyList<(string label, string url)>? buttons = null)
    {
        if (!_isConnected || _pipe == null) return;

        var cleanName = DiscordLimits.SanitizeText(activityName, DiscordLimits.MaxActivityNameBytes);
        var cleanDetails = DiscordLimits.SanitizeText(details, DiscordLimits.MaxTextBytes);
        var cleanState = DiscordLimits.SanitizeText(state, DiscordLimits.MaxTextBytes);
        var cleanLargeText = DiscordLimits.SanitizeText(largeImageText, DiscordLimits.MaxTextBytes);

        var assets = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(largeImageKey))
            assets["large_image"] = largeImageKey.Trim();
        if (!string.IsNullOrWhiteSpace(cleanLargeText))
            assets["large_text"] = cleanLargeText;

        var activity = new Dictionary<string, object?>
        {
            ["type"] = activityType
        };

        if (!string.IsNullOrWhiteSpace(cleanDetails))
            activity["details"] = cleanDetails;

        if (!string.IsNullOrWhiteSpace(cleanState))
            activity["state"] = cleanState;

        if (assets.Count > 0)
            activity["assets"] = assets;

        if (startTimestamp.HasValue)
        {
            var timestamps = new Dictionary<string, object>
            {
                ["start"] = ((DateTimeOffset)startTimestamp.Value).ToUnixTimeMilliseconds()
            };
            if (endTimestamp.HasValue && endTimestamp.Value > startTimestamp.Value)
                timestamps["end"] = ((DateTimeOffset)endTimestamp.Value).ToUnixTimeMilliseconds();
            activity["timestamps"] = timestamps;
        }

        if (!string.IsNullOrWhiteSpace(cleanName))
            activity["name"] = cleanName;

        // Add buttons if provided (Discord supports up to 2 buttons)
        if (buttons != null && buttons.Count > 0)
        {
            var btnList = new List<Dictionary<string, string>>();
            foreach (var (lbl, u) in buttons.Take(2))
            {
                var cleanLabel = DiscordLimits.SanitizeButtonLabel(lbl);
                var cleanUrl = DiscordLimits.SanitizeButtonUrl(u);
                if (!string.IsNullOrWhiteSpace(cleanLabel) && !string.IsNullOrWhiteSpace(cleanUrl))
                {
                    btnList.Add(new Dictionary<string, string>
                    {
                        ["label"] = cleanLabel,
                        ["url"] = cleanUrl
                    });
                }
            }
            if (btnList.Count > 0)
                activity["buttons"] = btnList.ToArray();
        }

        var payload = new
        {
            cmd = "SET_ACTIVITY",
            args = new
            {
                pid = Environment.ProcessId,
                activity = activity
            },
            nonce = Guid.NewGuid().ToString()
        };

        await SendFrameAsync(1, payload); // Opcode 1 = FRAME
    }

    public async Task ClearActivityAsync()
    {
        if (!_isConnected || _pipe == null) return;

        var payload = new
        {
            cmd = "SET_ACTIVITY",
            args = new
            {
                pid = Environment.ProcessId,
                activity = (object?)null
            },
            nonce = Guid.NewGuid().ToString()
        };

        await SendFrameAsync(1, payload);
    }

    private async Task SendFrameAsync(int opcode, object data)
    {
        if (_pipe == null || !_pipe.IsConnected) return;

        try
        {
            var json = JsonConvert.SerializeObject(data);
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            var frame = new byte[8 + jsonBytes.Length];
            BitConverter.GetBytes(opcode).CopyTo(frame, 0);
            BitConverter.GetBytes(jsonBytes.Length).CopyTo(frame, 4);
            jsonBytes.CopyTo(frame, 8);

            await _pipe.WriteAsync(frame, 0, frame.Length);
            await _pipe.FlushAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to send IPC frame: {ex.Message}");
        }
    }

    public void Disconnect()
    {
        _isConnected = false;
        _readCts?.Cancel();
        
        try { _pipe?.Close(); }
        catch (Exception ex) { Debug.WriteLine($"Pipe close error: {ex.Message}"); }

        try { _pipe?.Dispose(); }
        catch (Exception ex) { Debug.WriteLine($"Pipe dispose error: {ex.Message}"); }

        _pipe = null;
    }

    public void Dispose()
    {
        Disconnect();
        _readCts?.Dispose();
    }
}
