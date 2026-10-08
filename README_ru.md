# Discord SysInfo RPC

[English](README.md) | **Русский**

Небольшая утилита на C# / WPF, которая транслирует в статус Discord Rich Presence информацию о системе (нагрузку процессора, видеокарты, оперативки, температуры), а также название и иконку активной игры или программы.
<p align="center">
  <img src="https://github.com/user-attachments/assets/4788920f-4ab5-40e3-a58c-1c0a6948ec0f" alt="Discord SysInfo RPC Preview" width="450" />
</p>
## Основные возможности

- Мониторинг системы: опрос датчиков через LibreHardwareMonitor (нагрузка CPU/GPU/RAM, температуры, частоты и визуальная шкала).
- Подхват активного окна: определение игры или программы на переднем плане, получение читаемого названия из ярлыков и отображение её иконки в статусе.
- Кастомизация текста: можно оставить стандартную шкалу нагрузки железа либо написать свой текст с тегами (`{cpu}`, `{gpu_temp}`, `{ram_used}` и т.д.).
- Свои кнопки: поддержка до двух кликабельных кнопок в активности Discord.
- Иконки по уровню нагрузки: встроенные анимированные пресеты (Kiara, Gura, Mori, Discord Label) или свои ссылки под разные пороги (Idle, Low, Medium, High).
- Таймеры: время с запуска, с начала дня или полоса прогресса (для режима «Смотрит»).
- Сворачивание в трей, автоподключение и переключение языка интерфейса (RU / EN).
<p align="center">
  <img src="https://github.com/user-attachments/assets/ee546542-da21-451c-bf92-c3f5db9cd9a1" alt="Discord SysInfo RPC Preview in Discod" width="450" />
</p>
## Быстрый старт

1. Создайте приложение на [Discord Developer Portal](https://discord.com/developers/applications) (нажмите **New Application**).
2. Скопируйте **Application ID** и вставьте его в поле в программе.
3. Запустите программу от имени администратора (это требуется драйверу LibreHardwareMonitor для доступа к датчикам материнской платы и видеокарты).
4. Нажмите **Подключить**.

## Сборка из исходников

Для сборки требуется [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/Eranary/discord-sysinfo-rpc.git
cd discord-sysinfo-rpc

# Обычная сборка
dotnet build -c Release

# Сборка в один исполняемый файл (Single-file)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Собранный файл появится в `bin/Release/net8.0-windows/win-x64/publish/DiscordSysInfoRPC.exe`.

## Зависимости

- [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) — опрос системных датчиков и температур
- [Newtonsoft.Json](https://www.newtonsoft.com/json) — работа с форматом JSON и пакетами Discord IPC
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) — иконка и меню в системном трее
- [System.Drawing.Common](https://learn.microsoft.com/en-us/dotnet/api/system.drawing) — извлечение и обработка иконок процессов

## Лицензия

MIT License. См. файл [LICENSE](LICENSE).
