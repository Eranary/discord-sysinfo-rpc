using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DiscordSysInfoRPC;

public sealed class DetectedApp
{
    public string Name { get; init; } = "";
    public string ExePath { get; init; } = "";
}

public static class ForegroundAppDetector
{
    private static DetectedApp? _lastGood;
    private static ShortcutIndex? _shortcutIndex;
    private static DateTime _shortcutIndexAt;

    private sealed class ShortcutIndex
    {
        public Dictionary<string, string> ByPath { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ByFileName { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ByDirectory { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> IgnoredExe = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer",
        "searchhost",
        "searchapp",
        "shellexperiencehost",
        "startmenuexperiencehost",
        "textinputhost",
        "applicationframehost",
        "systemsettings",
        "lockapp",
        "logonui",
        "dwm",
        "sihost",
        "runtimebroker",
        "taskmgr",
        "mmc",
        "discordsysinforpc",
        "discordsysinforpc_full",
        "csrss",
        "winlogon",
        "fontdrvhost",
        "conhost",
        "screenclippinghost",
        "widgetboard",
        "widgets",
        "phoneexperiencehost"
    };

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public static DetectedApp? GetSticky(bool useShortcutName)
    {
        var current = TryReadForeground(useShortcutName);
        if (current != null)
            _lastGood = current;
        return _lastGood;
    }

    public static void RefreshAfterOptionChange(bool useShortcutName)
    {
        _shortcutIndex = null;
        if (_lastGood == null || string.IsNullOrWhiteSpace(_lastGood.ExePath))
            return;

        var exeName = Path.GetFileNameWithoutExtension(_lastGood.ExePath);
        if (string.IsNullOrWhiteSpace(exeName))
            return;

        var name = ResolveDisplayName(_lastGood.ExePath, exeName, useShortcutName);
        if (!string.IsNullOrWhiteSpace(name))
            _lastGood = new DetectedApp { Name = name, ExePath = _lastGood.ExePath };
    }

    private static DetectedApp? TryReadForeground(bool useShortcutName)
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;

            using var process = Process.GetProcessById((int)pid);
            string? exePath = null;
            try { exePath = process.MainModule?.FileName; }
            catch (Exception ex) { Debug.WriteLine($"Failed to get process MainModule: {ex.Message}"); }

            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                return null;

            var exeName = Path.GetFileNameWithoutExtension(exePath);
            if (string.IsNullOrWhiteSpace(exeName) || IgnoredExe.Contains(exeName))
                return null;

            var name = ResolveDisplayName(exePath, exeName, useShortcutName);
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return new DetectedApp { Name = name, ExePath = exePath };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetForegroundApp failed: {ex.Message}");
            return null;
        }
    }

    private static string ResolveDisplayName(string exePath, string exeName, bool useShortcutName)
    {
        string? fileDescription = null;
        string? productName = null;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            fileDescription = CleanName(info.FileDescription);
            productName = CleanName(info.ProductName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get FileVersionInfo for {exePath}: {ex.Message}");
        }

        if (IsUsefulName(fileDescription, exeName))
            return fileDescription!;
        if (IsUsefulName(productName, exeName))
            return productName!;

        if (useShortcutName && IsUsefulName(FindShortcutName(exePath), exeName, out var fromShortcut))
            return fromShortcut;

        return fileDescription ?? productName ?? CleanName(exeName) ?? exeName;
    }

    private static bool IsUsefulName(string? name, string exeName) =>
        !string.IsNullOrWhiteSpace(name) && !IsGenericName(name, exeName);

    private static bool IsUsefulName(string? name, string exeName, out string useful)
    {
        useful = name ?? "";
        return IsUsefulName(name, exeName);
    }

    private static bool IsGenericName(string name, string exeName)
    {
        var left = NormalizeToken(name);
        var right = NormalizeToken(exeName);
        return left == right || left == right + ".exe";
    }

    private static string NormalizeToken(string value) =>
        value.Trim().TrimEnd('.').Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();

    private static string? FindShortcutName(string exePath)
    {
        try
        {
            EnsureShortcutIndex();
            if (_shortcutIndex == null) return null;

            var full = NormalizePath(exePath);
            if (_shortcutIndex.ByPath.TryGetValue(full, out var byPath))
                return CleanName(byPath);

            var fileName = Path.GetFileName(full);
            if (!string.IsNullOrWhiteSpace(fileName)
                && _shortcutIndex.ByFileName.TryGetValue(fileName, out var byFile))
                return CleanName(byFile);

            var exeOnly = Path.GetFileNameWithoutExtension(full);
            if (!string.IsNullOrWhiteSpace(exeOnly)
                && _shortcutIndex.ByFileName.TryGetValue(exeOnly, out var byExe))
                return CleanName(byExe);

            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrWhiteSpace(dir)
                && _shortcutIndex.ByDirectory.TryGetValue(dir, out var byDir))
                return CleanName(byDir);

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FindShortcutName error: {ex.Message}");
            return null;
        }
    }

    private static void EnsureShortcutIndex()
    {
        if (_shortcutIndex != null && DateTime.UtcNow - _shortcutIndexAt < TimeSpan.FromMinutes(5))
            return;

        var index = new ShortcutIndex();
        foreach (var root in GetShortcutRoots())
            IndexShortcutRoot(index, root);

        _shortcutIndex = index;
        _shortcutIndexAt = DateTime.UtcNow;
    }

    private static IEnumerable<string> GetShortcutRoots()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop"),
            @"C:\Users\Public\Desktop"
        };

        return roots
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);
    }

    private static void IndexShortcutRoot(ShortcutIndex index, string root)
    {
        IEnumerable<string> links;
        try
        {
            links = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enumerate shortcuts in {root}: {ex.Message}");
            return;
        }

        using var enumerator = links.GetEnumerator();
        while (true)
        {
            try
            {
                if (!enumerator.MoveNext())
                    break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Shortcut enumeration stopped: {ex.Message}");
                break;
            }

            var link = enumerator.Current;
            try
            {
                var target = ReadShortcutTarget(link);
                if (string.IsNullOrWhiteSpace(target))
                    continue;

                var label = Path.GetFileNameWithoutExtension(link);
                if (string.IsNullOrWhiteSpace(label) || IsGenericName(label, Path.GetFileNameWithoutExtension(target)))
                    continue;

                var fullTarget = NormalizePath(target);
                index.ByPath[fullTarget] = label;

                var fileName = Path.GetFileName(fullTarget);
                if (!string.IsNullOrWhiteSpace(fileName))
                    index.ByFileName[fileName] = label;

                var exeOnly = Path.GetFileNameWithoutExtension(fullTarget);
                if (!string.IsNullOrWhiteSpace(exeOnly))
                    index.ByFileName[exeOnly] = label;

                var dir = Path.GetDirectoryName(fullTarget);
                if (!string.IsNullOrWhiteSpace(dir))
                    index.ByDirectory[dir] = label;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to parse shortcut {link}: {ex.Message}");
            }
        }
    }

    private static string? ReadShortcutTarget(string linkPath)
    {
        return ReadShortcutTargetShellLink(linkPath) ?? ReadShortcutTargetWscript(linkPath);
    }

    private static string? ReadShortcutTargetShellLink(string linkPath)
    {
        IShellLinkW? link = null;
        try
        {
            link = (IShellLinkW)new ShellLinkCoClass();
            ((IPersistFile)link).Load(linkPath, 0);
            var buffer = new StringBuilder(1024);
            link.GetPath(buffer, buffer.Capacity, out _, 0);
            var target = buffer.ToString().Trim();
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IShellLinkW failed for {linkPath}: {ex.Message}");
            return null;
        }
        finally
        {
            if (link != null)
                Marshal.FinalReleaseComObject(link);
        }
    }

    private static string? ReadShortcutTargetWscript(string linkPath)
    {
        object? shell = null;
        object? shortcut = null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return null;
            shell = Activator.CreateInstance(type);
            if (shell == null) return null;
            shortcut = type.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { linkPath });
            if (shortcut == null) return null;
            var target = shortcut.GetType().InvokeMember(
                "TargetPath",
                System.Reflection.BindingFlags.GetProperty,
                null,
                shortcut,
                null) as string;
            return string.IsNullOrWhiteSpace(target) ? null : target.Trim();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WScript.Shell failed for {linkPath}: {ex.Message}");
            return null;
        }
        finally
        {
            if (shortcut != null)
                Marshal.FinalReleaseComObject(shortcut);
            if (shell != null)
                Marshal.FinalReleaseComObject(shell);
        }
    }

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception ex)
        {
            Debug.WriteLine($"NormalizePath failed for {path}: {ex.Message}");
            return path;
        }
    }

    private static string? CleanName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Equals("unknown", StringComparison.OrdinalIgnoreCase)) return null;
        return trimmed;
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass
    {
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Win32FindDataW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, out Win32FindDataW pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
