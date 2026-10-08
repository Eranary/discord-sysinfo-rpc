using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace DiscordSysInfoRPC;

public static class IconUploadService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly ConcurrentDictionary<string, string> Cache = LoadCache();
    private static readonly ConcurrentDictionary<string, DateTime> FailUntil = new();
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly SemaphoreSlim TokenGate = new(1, 1);

    private static string? _currentToken;
    private static DateTime _tokenExpiresAt = DateTime.MinValue;

    private static readonly string CachePath = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? ".",
        "icon-cache.json"
    );

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        return client;
    }

    public static string? GetCachedUrl(string sha256)
    {
        return Cache.TryGetValue(sha256, out var url) ? url : null;
    }

    public static string ComputeSha256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task<string?> ResolveUrlAsync(byte[] png, CancellationToken ct = default)
    {
        if (png.Length == 0) return null;

        var hash = ComputeSha256(png);
        if (Cache.TryGetValue(hash, out var cached))
            return cached;

        if (FailUntil.TryGetValue(hash, out var until) && until > DateTime.UtcNow)
            return null;

        if (!AppConstants.CanUploadIcons)
            return null;

        await Gate.WaitAsync(ct);
        try
        {
            if (Cache.TryGetValue(hash, out cached))
                return cached;

            var publicUrl = AppConstants.IconCdnBase.TrimEnd('/') + "/" + hash + ".png";
            if (await ExistsAsync(publicUrl, ct))
            {
                Remember(hash, publicUrl);
                return publicUrl;
            }

            var uploaded = await UploadAsync(png, hash, ct);
            if (!string.IsNullOrWhiteSpace(uploaded))
            {
                Remember(hash, uploaded);
                return uploaded;
            }

            FailUntil[hash] = DateTime.UtcNow.AddMinutes(10);
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to resolve icon {hash}: {ex.Message}");
            FailUntil[hash] = DateTime.UtcNow.AddMinutes(10);
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<bool> ExistsAsync(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            using var resp = await Http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Icon ExistsAsync failed for {url}: {ex.Message}");
            return false;
        }
    }

    private static async Task<string?> GetOrRefreshTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_currentToken) && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-2))
            return _currentToken;

        await TokenGate.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_currentToken) && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-2))
                return _currentToken;

            using var req = new HttpRequestMessage(HttpMethod.Get, AppConstants.IconTokenUrl);
            using var resp = await Http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return null;

            var body = await resp.Content.ReadAsStringAsync(ct);
            var json = JObject.Parse(body);
            var token = json["token"]?.ToString();
            var expiresIn = json["expires_in"]?.Value<int>() ?? 900;

            if (string.IsNullOrWhiteSpace(token))
                return null;

            _currentToken = token;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
            return token;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetOrRefreshTokenAsync failed: {ex.Message}");
            return null;
        }
        finally
        {
            TokenGate.Release();
        }
    }

    private static async Task<string?> UploadAsync(byte[] png, string hash, CancellationToken ct)
    {
        var token = await GetOrRefreshTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "icon", hash + ".png");

        using var req = new HttpRequestMessage(HttpMethod.Post, AppConstants.IconUploadUrl);
        req.Headers.TryAddWithoutValidation("X-Rpc-Token", token);
        req.Content = content;

        using var resp = await Http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _currentToken = null;
            _tokenExpiresAt = DateTime.MinValue;
            return null;
        }

        if (!resp.IsSuccessStatusCode)
            return null;

        var body = await resp.Content.ReadAsStringAsync(ct);
        try
        {
            var json = JObject.Parse(body);
            return json["url"]?.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to parse upload JSON response: {ex.Message}");
            return null;
        }
    }

    private static void Remember(string hash, string url)
    {
        Cache[hash] = url;
        try
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(Cache, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(CachePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write icon cache: {ex.Message}");
        }
    }

    private static ConcurrentDictionary<string, string> LoadCache()
    {
        try
        {
            if (File.Exists(CachePath))
            {
                var json = File.ReadAllText(CachePath);
                var map = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (map != null)
                    return new ConcurrentDictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read icon cache: {ex.Message}");
        }

        return new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
