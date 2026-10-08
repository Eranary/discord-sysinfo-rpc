using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace DiscordSysInfoRPC;

public static class ExeIconExtractor
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint PrivateExtractIcons(
        string lpszFile, int nIconIndex, int cxIcon, int cyIcon,
        IntPtr[] phicon, uint[] piconid, uint nIcons, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static byte[]? TryGetPng(string exePath)
    {
        foreach (var size in new[] { 256, 128, 64, 48, 32 })
        {
            var png = TryExtract(exePath, size);
            if (png != null && png.Length > 0)
                return png;
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            return icon == null ? null : ToPng(icon);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ExtractAssociatedIcon failed for {exePath}: {ex.Message}");
            return null;
        }
    }

    private static byte[]? TryExtract(string exePath, int size)
    {
        var icons = new IntPtr[1];
        var ids = new uint[1];
        try
        {
            var count = PrivateExtractIcons(exePath, 0, size, size, icons, ids, 1, 0);
            if (count == 0 || icons[0] == IntPtr.Zero)
                return null;

            using var icon = Icon.FromHandle(icons[0]);
            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PrivateExtractIcons failed for {exePath} ({size}x{size}): {ex.Message}");
            return null;
        }
        finally
        {
            if (icons[0] != IntPtr.Zero)
                DestroyIcon(icons[0]);
        }
    }

    private static byte[] ToPng(Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
