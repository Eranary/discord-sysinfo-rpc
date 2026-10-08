# Discord SysInfo RPC

**English** | [Русский](README_ru.md)

A lightweight C# / WPF utility that displays real-time hardware metrics (CPU, GPU, RAM, temperatures) and the currently focused window or game in your Discord Rich Presence status.
<p align="center">
  <img src="https://github.com/user-attachments/assets/4788920f-4ab5-40e3-a58c-1c0a6948ec0f" alt="Discord SysInfo RPC Preview" width="450" />
</p>

## Features

- Hardware monitoring: live sensor data via LibreHardwareMonitor (CPU, GPU, RAM usage, clock speeds, temperatures, and text progress bars).
- Active window detection: identifies the foreground application or game, resolves user-friendly shortcut names, and displays its icon in Discord.
- Custom status text: customize activity details and state using placeholders (`{cpu}`, `{gpu_temp}`, `{ram_used}`, `{bar}`, etc.).
- Custom buttons: up to two clickable buttons linking to your website, stream, or profile.
- Reactive load icons: built-in animated character presets (Kiara, Gura, Mori, Discord Label) or custom image URLs mapped to load thresholds (Idle, Low, Medium, High).
- Flexible timers: elapsed time, time since start of day, or watching duration progress bar.
- System tray minimization, auto-connect on launch, and bilingual UI (English / Russian).
<img width="846" height="280" alt="image" src="https://github.com/user-attachments/assets/ee546542-da21-451c-bf92-c3f5db9cd9a1" />

## Quick Start

1. Create a new app on the [Discord Developer Portal](https://discord.com/developers/applications) by clicking **New Application**.
2. Copy your **Application ID** and paste it into the application.
3. Run `DiscordSysInfoRPC.exe` as **Administrator** (required by LibreHardwareMonitor to access hardware sensor drivers).
4. Click **Connect**.

## Building from Source

Requires [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows 10/11 (x64).

```bash
git clone https://github.com/Eranary/discord-sysinfo-rpc.git
cd discord-sysinfo-rpc

# Build
dotnet build -c Release

# Publish as a standalone single-file executable
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The resulting executable will be in `bin/Release/net8.0-windows/win-x64/publish/DiscordSysInfoRPC.exe`.

## Dependencies

- [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) — hardware sensor monitoring
- [Newtonsoft.Json](https://www.newtonsoft.com/json) — JSON serialization and Discord IPC framing
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) — Windows notification tray icon and context menu
- [System.Drawing.Common](https://learn.microsoft.com/en-us/dotnet/api/system.drawing) — executable icon extraction and bitmap processing

## License

MIT License. See [LICENSE](LICENSE).
