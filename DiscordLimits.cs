using System.Text;

namespace DiscordSysInfoRPC;

public static class DiscordLimits
{
    public const int MaxTextBytes = 128;
    public const int MaxDetailsBytes = 128;
    public const int MaxStateBytes = 128;
    public const int MaxLargeTextBytes = 128;
    public const int MaxButtonLabelBytes = 32;
    public const int MaxButtonUrlBytes = 512;
    public const int MaxActivityNameBytes = 128;

    /// <summary>
    /// Sanitizes text for Discord activity fields (details, state, name, large_text).
    /// Ensures 2..128 bytes range without violating Discord RPC limits.
    /// </summary>
    public static string? SanitizeText(string? value, int maxBytes = MaxTextBytes)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        var truncated = TruncateUtf8Bytes(trimmed, maxBytes);
        if (string.IsNullOrWhiteSpace(truncated))
            return null;

        // Discord requires at least 2 characters for details, state and name
        if (truncated.Length == 1)
            truncated += " ";

        return truncated;
    }

    /// <summary>
    /// Sanitizes and validates button label (1..32 bytes).
    /// </summary>
    public static string? SanitizeButtonLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        var trimmed = label.Trim();
        var truncated = TruncateUtf8Bytes(trimmed, MaxButtonLabelBytes);
        if (string.IsNullOrWhiteSpace(truncated))
            return null;

        if (truncated.Length == 1)
            truncated += " ";

        return truncated;
    }

    /// <summary>
    /// Validates and normalizes button URL. Returns null if invalid or unsafe.
    /// </summary>
    public static string? SanitizeButtonUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var trimmed = url.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        if (Encoding.UTF8.GetByteCount(trimmed) > MaxButtonUrlBytes)
            return null;

        return trimmed;
    }

    public static string TruncateUtf8Bytes(string value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= maxBytes) return value;

        var length = value.Length;
        while (length > 0)
        {
            length--;
            if (Encoding.UTF8.GetByteCount(value.AsSpan(0, length)) <= maxBytes)
                return value[..length];
        }

        return "";
    }
}
