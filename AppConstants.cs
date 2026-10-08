namespace DiscordSysInfoRPC;

/// <summary>
/// Application constants, developer links, and service endpoints.
/// </summary>
public static class AppConstants
{
    public const string AppName = "Discord SysInfo RPC";
    public const string Version = "1.1.0";

    // Developers
    public const string DeveloperName = "@Eranary";
    public const string DeveloperDiscordUrl = "https://discord.com/users/103906051014791168";
    public const string DeveloperName2 = "@Alen";
    public const string DeveloperDiscordUrl2 = "https://discord.com/users/319229722703953930";
    public const string ProjectUrl = "https://disflare.com";

    // Optional promotional button in activity
    public const string PromoButtonLabel = "✨ Хочу такой же";
    public const string PromoButtonUrl = "https://disflare.com";

    // Process icon upload CDN endpoints
    public const string IconTokenUrl = "https://disflare.com/api/rpc-token";
    public const string IconUploadUrl = "https://disflare.com/api/rpc-icon";
    public const string IconCdnBase = "https://disflare.com/rpc-icons/";
    public static bool CanUploadIcons => true;
}
