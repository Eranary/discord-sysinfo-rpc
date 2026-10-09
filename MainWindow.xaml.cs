using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DiscordSysInfoRPC;

public class InfoItem
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "—";
}

public partial class MainWindow : Window
{
    private Config _config;
    private SystemInfoService? _systemInfo;
    private RpcService? _rpcService;
    private DispatcherTimer _timer;
    private ObservableCollection<InfoItem> _sysInfo;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _helpExpanded;
    private bool _systemExpanded;
    private bool _loadingUi = true;

    private static readonly Brush SuccessBrush = new SolidColorBrush(Color.FromRgb(59, 165, 92));
    private static readonly Brush DangerBrush = new SolidColorBrush(Color.FromRgb(237, 66, 69));
    private static readonly Brush TextSecondaryBrush = new SolidColorBrush(Color.FromRgb(185, 187, 190));

    public MainWindow()
    {
        _loadingUi = true;
        InitializeComponent();
        _config = Config.Load();
        Loc.SetLanguage(_config.UiLanguage);

        DataObject.AddPastingHandler(WatchingHoursBox, OnNumericPaste);
        DataObject.AddPastingHandler(WatchingMinutesBox, OnNumericPaste);

        _sysInfo = new ObservableCollection<InfoItem>();
        InitSystemInfoLabels();
        SystemInfoList.ItemsSource = _sysInfo;

        ApplyLocalization();

        try
        {
            _systemInfo = new SystemInfoService();
            _rpcService = new RpcService(_config, _systemInfo);
            _rpcService.OnConnectionChanged += OnConnectionChanged;
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.Format("InitError", ex.Message), Loc.Get("InitErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => UpdateSystemInfo();
        _timer.Start();

        LoadConfigToUI();
        UpdateSystemInfo();
        InitTray();

        if (_config.AutoConnect && !string.IsNullOrWhiteSpace(_config.ClientId) && _rpcService != null)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(1000);
                Connect();
            });
        }
    }

    private void InitSystemInfoLabels()
    {
        _sysInfo.Clear();
        _sysInfo.Add(new InfoItem { Label = Loc.Get("Cpu") });
        _sysInfo.Add(new InfoItem { Label = Loc.Get("CpuLoad") });
        _sysInfo.Add(new InfoItem { Label = Loc.Get("CpuTemp") });
        _sysInfo.Add(new InfoItem { Label = Loc.Get("Gpu") });
        _sysInfo.Add(new InfoItem { Label = Loc.Get("GpuLoad") });
        _sysInfo.Add(new InfoItem { Label = Loc.Get("GpuTemp") });
        _sysInfo.Add(new InfoItem { Label = Loc.Get("Ram") });
        _sysInfo.Add(new InfoItem { Label = Loc.Get("RamUsed") });
    }

    private void ApplyLocalization()
    {
        var prevLoading = _loadingUi;
        _loadingUi = true;
        try
        {
            Title = Loc.Get("AppTitle");
            HeaderTitle.Text = Loc.Get("AppTitle");
            SettingsSectionTitle.Text = Loc.Get("SectionSettings");
            AdditionalSectionTitle.Text = Loc.Get("SectionAdditional");
            SystemToggle.Text = _systemExpanded ? Loc.Get("SystemExpanded") : Loc.Get("System");
            LabelAppId.Text = Loc.Get("LabelAppId");
            ClientIdPlaceholder.Text = Loc.Get("PlaceholderAppId");
            UpdateClientIdPlaceholder();
            LabelActivityName.Text = Loc.Get("LabelActivityName");
            ActivityNamePlaceholder.Text = Loc.Get("PlaceholderActivityName");
            ButtonLabelPlaceholder.Text = Loc.Get("PlaceholderButtonLabel");
            ButtonUrlPlaceholder.Text = Loc.Get("PlaceholderButtonUrl");
            UpdateActivityNamePlaceholder();
            UpdateButtonLabelPlaceholder();
            UpdateButtonUrlPlaceholder();
            LabelActivityType.Text = Loc.Get("LabelActivityType");
            LabelLanguage.Text = Loc.Get("LabelLanguage");
            LabelImageSet.Text = Loc.Get("LabelImageSet");
            LabelTimerMode.Text = Loc.Get("LabelTimerMode");
            LabelWatchingDuration.Text = Loc.Get("LabelWatchingDuration");
            LabelWatchingCustomDuration.Text = Loc.Get("LabelWatchingCustomDuration");
            LabelWatchingHoursUnit.Text = Loc.Get("UnitHours");
            LabelWatchingMinutesUnit.Text = Loc.Get("UnitMinutes");
            WatchingDurationHint.Text = Loc.Get("WatchingDurationHint");
            WatchingHoursPlaceholder.Text = "0";
            WatchingMinutesPlaceholder.Text = "0";
            UpdateWatchingHoursPlaceholder();
            UpdateWatchingMinutesPlaceholder();
            LabelUpdateInterval.Text = Loc.Get("LabelUpdateInterval");
            AutoConnectBox.Content = Loc.Get("AutoConnect");
            DetectAppBox.Content = Loc.Get("DetectForegroundApp");
            DetectAppHint.Text = Loc.Get("DetectForegroundAppHint");
            UseShortcutNameBox.Content = Loc.Get("UseShortcutName");
            UseShortcutNameHint.Text = Loc.Get("UseShortcutNameHint");
            CustomStatusTextBox.Content = Loc.Get("CustomStatusText");
            CustomStatusTextHint.Text = Loc.Get("CustomStatusTextHint");
            LabelCustomDetails.Text = Loc.Get("LabelCustomDetails");
            CustomDetailsPlaceholder.Text = Loc.Get("PlaceholderCustomDetails");
            LabelCustomState.Text = Loc.Get("LabelCustomState");
            CustomStatePlaceholder.Text = Loc.Get("PlaceholderCustomState");
            CustomStatusTagsHint.Text = Loc.Get("CustomStatusTagsHint");
            UpdateCustomDetailsPlaceholder();
            UpdateCustomStatePlaceholder();
            ShowPromoBox.Content = Loc.Get("ShowPromoButton");
            ShowPromoHint.Text = Loc.Get("ShowPromoButtonHint");
            ShowButtonBox.Content = Loc.Get("ShowButton");
            LabelButtonText.Text = Loc.Get("LabelButtonText");
            LabelButtonUrl.Text = Loc.Get("LabelButtonUrl");
            HelpToggle.Text = _helpExpanded ? Loc.Get("HelpExpanded") : Loc.Get("Help");
            HelpDevPortalTitle.Text = Loc.Get("HelpDevPortalTitle");
            HelpDevPortal1.Text = Loc.Get("HelpDevPortal1");
            HelpDevPortal2.Text = Loc.Get("HelpDevPortal2");
            HelpDevPortal3.Text = Loc.Get("HelpDevPortal3");
            DevPortalLink.Text = Loc.Get("HelpDevPortalLink");
            HelpServerTitle.Text = Loc.Get("HelpServerTitle");
            HelpServerHint.Text = Loc.Get("HelpServerHint");
            ServerLink.Text = Loc.Get("HelpServerLink");
            DisconnectBtn.Content = Loc.Get("Disconnect");
            ConnectBtn.Content = Loc.Get("Connect");
            SaveBtn.Content = Loc.Get("Save");
            FooterPrefixRun.Text = Loc.Get("FooterPrefix");
            DeveloperNameRun.Text = AppConstants.DeveloperName;
            DeveloperLink.NavigateUri = new Uri(AppConstants.DeveloperDiscordUrl);
            DeveloperName2Run.Text = AppConstants.DeveloperName2;
            DeveloperLink2.NavigateUri = new Uri(AppConstants.DeveloperDiscordUrl2);

            var isConnected = _rpcService?.IsConnected ?? false;
            SetStatusText(isConnected);

            var actIdx = ActivityTypeBox.SelectedIndex;
            ActivityTypeBox.Items.Clear();
            ActivityTypeBox.Items.Add(Loc.Get("ActivityPlaying"));
            ActivityTypeBox.Items.Add(Loc.Get("ActivityListening"));
            ActivityTypeBox.Items.Add(Loc.Get("ActivityWatching"));
            ActivityTypeBox.Items.Add(Loc.Get("ActivityCompeting"));
            if (actIdx >= 0 && actIdx < ActivityTypeBox.Items.Count) ActivityTypeBox.SelectedIndex = actIdx;

            var imgIdx = ImageSetBox.SelectedIndex;
            ImageSetBox.Items.Clear();
            ImageSetBox.Items.Add(Loc.Get("ImageKiara"));
            ImageSetBox.Items.Add(Loc.Get("ImageGura"));
            ImageSetBox.Items.Add(Loc.Get("ImageMori"));
            ImageSetBox.Items.Add(Loc.Get("ImageDiscordLabel"));
            ImageSetBox.Items.Add(Loc.Get("ImageCustom"));
            if (imgIdx >= 0 && imgIdx < ImageSetBox.Items.Count) ImageSetBox.SelectedIndex = imgIdx;

            var tmIdx = TimestampModeBox.SelectedIndex;
            TimestampModeBox.Items.Clear();
            TimestampModeBox.Items.Add(Loc.Get("TimerElapsed"));
            TimestampModeBox.Items.Add(Loc.Get("TimerStartOfDay"));
            if (tmIdx >= 0 && tmIdx < TimestampModeBox.Items.Count) TimestampModeBox.SelectedIndex = tmIdx;

            var wdIdx = WatchingDurationBox.SelectedIndex;
            WatchingDurationBox.Items.Clear();
            WatchingDurationBox.Items.Add(Loc.Get("TimerElapsed"));
            WatchingDurationBox.Items.Add(Loc.Get("TimerStartOfDay"));
            WatchingDurationBox.Items.Add(Loc.Get("WatchProgressBar"));
            if (wdIdx >= 0 && wdIdx < WatchingDurationBox.Items.Count)
                WatchingDurationBox.SelectedIndex = wdIdx;
            else
                WatchingDurationBox.SelectedIndex = MapWatchingTimerToIndex(_config.WatchingTimerMode);

            var uiIdx = UpdateIntervalBox.SelectedIndex;
            UpdateIntervalBox.Items.Clear();
            UpdateIntervalBox.Items.Add(Loc.Get("Interval5"));
            UpdateIntervalBox.Items.Add(Loc.Get("Interval10"));
            UpdateIntervalBox.Items.Add(Loc.Get("Interval15"));
            UpdateIntervalBox.Items.Add(Loc.Get("Interval30"));
            if (uiIdx >= 0 && uiIdx < UpdateIntervalBox.Items.Count) UpdateIntervalBox.SelectedIndex = uiIdx;

            var langIdx = LanguageBox.SelectedIndex;
            LanguageBox.Items.Clear();
            LanguageBox.Items.Add(Loc.Get("LangRu"));
            LanguageBox.Items.Add(Loc.Get("LangEn"));
            LanguageBox.SelectedIndex = _config.UiLanguage == "en" ? 1 : 0;
            if (langIdx >= 0 && langIdx < 2) LanguageBox.SelectedIndex = langIdx;
        }
        finally
        {
            _loadingUi = prevLoading;
        }
    }

    private void InitTray()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = Loc.Get("AppTitle"),
            Visible = true
        };

        try
        {
            System.Drawing.Icon? loadedIcon = null;

            // 1. Try loading from file in AppContext.BaseDirectory
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "icodiscord.ico");
            if (System.IO.File.Exists(iconPath))
            {
                try
                {
                    loadedIcon = new System.Drawing.Icon(iconPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed loading icon from file: {ex.Message}");
                }
            }

            // 2. Try loading from embedded WPF resource stream
            if (loadedIcon == null)
            {
                try
                {
                    var iconUri = new Uri("pack://application:,,,/icodiscord.ico", UriKind.Absolute);
                    var sri = System.Windows.Application.GetResourceStream(iconUri);
                    if (sri?.Stream != null)
                    {
                        using (sri.Stream)
                        {
                            loadedIcon = new System.Drawing.Icon(sri.Stream);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed loading icon from resource stream: {ex.Message}");
                }
            }

            // 3. Try extracting associated icon from the executable
            if (loadedIcon == null)
            {
                try
                {
                    var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                    {
                        loadedIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed extracting icon from exe: {ex.Message}");
                }
            }

            _trayIcon.Icon = loadedIcon ?? System.Drawing.SystemIcons.Application;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load tray icon: {ex.Message}");
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        UpdateTrayMenu();
        _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); };
    }

    private void UpdateTrayMenu()
    {
        if (_trayIcon == null) return;
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(Loc.Get("TrayOpen"), null, (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); });
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(Loc.Get("TrayExit"), null, (_, _) =>
        {
            if (!_loadingUi)
            {
                SaveConfigFromUI();
                _config.Save();
            }
            _trayIcon.Visible = false;
            Application.Current.Shutdown();
        });
        _trayIcon.ContextMenuStrip = menu;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_loadingUi)
        {
            SaveConfigFromUI();
            _config.Save();
        }
        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_loadingUi)
        {
            SaveConfigFromUI();
            _config.Save();
        }
        _timer?.Stop();
        _rpcService?.Dispose();
        _systemInfo?.Dispose();
        _trayIcon?.Dispose();
        base.OnClosed(e);
    }

    private void UpdateClientIdPlaceholder()
    {
        ClientIdPlaceholder.Visibility = string.IsNullOrWhiteSpace(ClientIdBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ClientIdBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateClientIdPlaceholder();

    private void ActivityNameBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateActivityNamePlaceholder();

    private void ButtonLabelBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateButtonLabelPlaceholder();

    private void ButtonUrlBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateButtonUrlPlaceholder();

    private void UpdateActivityNamePlaceholder() =>
        ActivityNamePlaceholder.Visibility = string.IsNullOrWhiteSpace(ActivityNameBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void UpdateButtonLabelPlaceholder() =>
        ButtonLabelPlaceholder.Visibility = string.IsNullOrWhiteSpace(ButtonLabelBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void UpdateButtonUrlPlaceholder() =>
        ButtonUrlPlaceholder.Visibility = string.IsNullOrWhiteSpace(ButtonUrlBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void CustomDetailsBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateCustomDetailsPlaceholder();

    private void CustomStateBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateCustomStatePlaceholder();

    private void UpdateCustomDetailsPlaceholder()
    {
        if (CustomDetailsPlaceholder != null && CustomDetailsBox != null)
            CustomDetailsPlaceholder.Visibility = string.IsNullOrWhiteSpace(CustomDetailsBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void UpdateCustomStatePlaceholder()
    {
        if (CustomStatePlaceholder != null && CustomStateBox != null)
            CustomStatePlaceholder.Visibility = string.IsNullOrWhiteSpace(CustomStateBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void CustomStatusText_Changed(object sender, RoutedEventArgs e)
    {
        UpdateCustomStatusTextPanel();
        Option_Changed(sender, e);
    }

    private void UpdateCustomStatusTextPanel()
    {
        if (CustomStatusTextPanel == null || CustomStatusTextBox == null) return;
        CustomStatusTextPanel.Visibility = CustomStatusTextBox.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ActivityTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActivityTypeBox.SelectedIndex < 0) return;
        var actType = IndexToActivityType(ActivityTypeBox.SelectedIndex);
        var isWatching = actType == 3;
        TimerModePanel.Visibility = isWatching ? Visibility.Collapsed : Visibility.Visible;
        WatchingDurationPanel.Visibility = isWatching ? Visibility.Visible : Visibility.Collapsed;
        if (isWatching && WatchingCustomDurationPanel != null)
        {
            WatchingCustomDurationPanel.Visibility = WatchingDurationBox.SelectedIndex == 2
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (!_loadingUi)
        {
            SaveConfigFromUI();
            _config.Save();
            _rpcService?.UpdateConfig(_config);
        }
    }

    private void Option_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingUi) return;
        SaveConfigFromUI();
        _config.Save();
        _rpcService?.UpdateConfig(_config);
    }

    private void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loadingUi) return;
        SaveConfigFromUI();
        _config.Save();
    }

    private void WatchingDurationBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WatchingCustomDurationPanel != null)
        {
            var isProgress = WatchingDurationBox.SelectedIndex == 2;
            WatchingCustomDurationPanel.Visibility = isProgress ? Visibility.Visible : Visibility.Collapsed;
        }

        if (!_loadingUi)
        {
            SaveConfigFromUI();
            _config.Save();
            _rpcService?.UpdateConfig(_config);
        }
    }

    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
    }

    private void NumericTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
            e.Handled = true;
    }

    private void OnNumericPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            if (!Regex.IsMatch(text, "^[0-9]+$"))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void WatchingHoursBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SanitizeNumericBox(WatchingHoursBox);
        UpdateWatchingHoursPlaceholder();
        if (!_loadingUi)
        {
            ApplyWatchingDurationChange();
        }
    }

    private void WatchingMinutesBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SanitizeNumericBox(WatchingMinutesBox);
        UpdateWatchingMinutesPlaceholder();
        if (!_loadingUi)
        {
            ApplyWatchingDurationChange();
        }
    }

    private void UpdateWatchingHoursPlaceholder()
    {
        if (WatchingHoursPlaceholder != null && WatchingHoursBox != null)
            WatchingHoursPlaceholder.Visibility = string.IsNullOrWhiteSpace(WatchingHoursBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void UpdateWatchingMinutesPlaceholder()
    {
        if (WatchingMinutesPlaceholder != null && WatchingMinutesBox != null)
            WatchingMinutesPlaceholder.Visibility = string.IsNullOrWhiteSpace(WatchingMinutesBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private static void SanitizeNumericBox(TextBox box)
    {
        var text = box.Text;
        var clean = Regex.Replace(text, "[^0-9]", "");
        if (clean != text)
        {
            var caret = box.CaretIndex;
            box.Text = clean;
            box.CaretIndex = Math.Min(caret, clean.Length);
        }
    }

    private void ApplyWatchingDurationChange()
    {
        int.TryParse(WatchingHoursBox.Text, out var h);
        int.TryParse(WatchingMinutesBox.Text, out var m);

        h = Math.Clamp(h, 0, 24);
        m = Math.Clamp(m, 0, 59);

        var totalMinutes = Math.Clamp(h * 60 + m, 1, 1440);
        _config.WatchingDurationMinutes = totalMinutes;
        _rpcService?.UpdateConfig(_config);
    }

    private void WatchingTimeBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loadingUi) return;
        int.TryParse(WatchingHoursBox.Text, out var h);
        int.TryParse(WatchingMinutesBox.Text, out var m);

        h = Math.Clamp(h, 0, 24);
        m = Math.Clamp(m, 0, 59);

        if (h == 0 && m == 0)
        {
            m = 1; // Minimum 1 minute
        }

        WatchingHoursBox.Text = h.ToString();
        WatchingMinutesBox.Text = m.ToString();
        ApplyWatchingDurationChange();
        _config.Save();
    }

    private void LoadConfigToUI()
    {
        _loadingUi = true;
        try
        {
            ClientIdBox.Text = _config.ClientId;
            ActivityNameBox.Text = _config.ActivityName;
            ActivityTypeBox.SelectedIndex = ActivityTypeToIndex(_config.ActivityType);
            ImageSetBox.SelectedIndex = MapImageSetToIndex(_config.ImageSet);
            TimestampModeBox.SelectedIndex = _config.TimestampMode == "startOfDay" ? 1 : 0;
            WatchingDurationBox.SelectedIndex = MapWatchingTimerToIndex(_config.WatchingTimerMode);
            var totalMinutes = Math.Clamp(_config.WatchingDurationMinutes, 1, 1440);
            _config.WatchingDurationMinutes = totalMinutes;
            var watchHours = totalMinutes / 60;
            var watchMinutes = totalMinutes % 60;
            WatchingHoursBox.Text = watchHours.ToString();
            WatchingMinutesBox.Text = watchMinutes.ToString();
            UpdateWatchingHoursPlaceholder();
            UpdateWatchingMinutesPlaceholder();

            UpdateIntervalBox.SelectedIndex = _config.UpdateInterval switch { 5000 => 0, 10000 => 1, 15000 => 2, 30000 => 3, _ => 1 };
            AutoConnectBox.IsChecked = _config.AutoConnect;
            LanguageBox.SelectedIndex = _config.UiLanguage == "en" ? 1 : 0;

            DetectAppBox.IsChecked = _config.DetectForegroundApp;
            UseShortcutNameBox.IsChecked = _config.UseShortcutName;
            CustomStatusTextBox.IsChecked = _config.CustomStatusText;
            CustomDetailsBox.Text = _config.CustomDetails;
            CustomStateBox.Text = _config.CustomState;
            UpdateCustomStatusTextPanel();

            ShowPromoBox.IsChecked = _config.ShowPromoButton;
            ShowButtonBox.IsChecked = _config.ShowButton;
            ButtonLabelBox.Text = _config.ButtonLabel;
            ButtonUrlBox.Text = _config.ButtonUrl;

            UpdateClientIdPlaceholder();
            UpdateActivityNamePlaceholder();
            UpdateButtonLabelPlaceholder();
            UpdateButtonUrlPlaceholder();
            UpdateCustomDetailsPlaceholder();
            UpdateCustomStatePlaceholder();

            var actType = _config.ActivityType;
            var isWatching = actType == 3;
            TimerModePanel.Visibility = isWatching ? Visibility.Collapsed : Visibility.Visible;
            WatchingDurationPanel.Visibility = isWatching ? Visibility.Visible : Visibility.Collapsed;
            WatchingCustomDurationPanel.Visibility = (isWatching && _config.WatchingTimerMode == "progress")
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        finally
        {
            _loadingUi = false;
        }
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingUi) return;
        SaveConfigFromUI();
        _config.Save();
        ForegroundAppDetector.RefreshAfterOptionChange(_config.UseShortcutName);
        _rpcService?.UpdateConfig(_config);
    }

    private void SaveConfigFromUI()
    {
        _config.ClientId = ClientIdBox.Text.Trim();
        _config.ActivityName = ActivityNameBox.Text.Trim();
        _config.ActivityType = IndexToActivityType(ActivityTypeBox.SelectedIndex);
        _config.ImageSet = MapIndexToImageSet(ImageSetBox.SelectedIndex);
        _config.TimestampMode = TimestampModeBox.SelectedIndex == 1 ? "startOfDay" : "elapsed";
        _config.WatchingTimerMode = MapIndexToWatchingTimer(WatchingDurationBox.SelectedIndex);
        int.TryParse(WatchingHoursBox.Text, out var h);
        int.TryParse(WatchingMinutesBox.Text, out var m);
        _config.WatchingDurationMinutes = Math.Clamp(Math.Clamp(h, 0, 24) * 60 + Math.Clamp(m, 0, 59), 1, 1440);
        _config.UpdateInterval = UpdateIntervalBox.SelectedIndex switch { 0 => 5000, 1 => 10000, 2 => 15000, 3 => 30000, _ => 10000 };
        _config.AutoConnect = AutoConnectBox.IsChecked == true;
        _config.UiLanguage = LanguageBox.SelectedIndex == 1 ? "en" : "ru";

        _config.DetectForegroundApp = DetectAppBox.IsChecked == true;
        _config.UseShortcutName = UseShortcutNameBox.IsChecked == true;
        _config.CustomStatusText = CustomStatusTextBox.IsChecked == true;
        _config.CustomDetails = CustomDetailsBox.Text.Trim();
        _config.CustomState = CustomStateBox.Text.Trim();
        _config.ShowPromoButton = ShowPromoBox.IsChecked == true;
        _config.ShowButton = ShowButtonBox.IsChecked == true;
        _config.ButtonLabel = ButtonLabelBox.Text.Trim();
        _config.ButtonUrl = ButtonUrlBox.Text.Trim();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveConfigFromUI();
        _config.Save();
        _rpcService?.UpdateConfig(_config);

        var originalText = SaveBtn.Content;
        SaveBtn.Content = Loc.Get("Saved");
        SaveBtn.IsEnabled = false;
        await Task.Delay(1500);
        SaveBtn.Content = originalText;
        SaveBtn.IsEnabled = true;
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedIndex < 0) return;
        var lang = LanguageBox.SelectedIndex == 1 ? "en" : "ru";
        if (_config.UiLanguage == lang) return;

        _config.UiLanguage = lang;
        Loc.SetLanguage(lang);
        ApplyLocalization();
        _config.Save();
    }

    private void Connect_Click(object sender, RoutedEventArgs e) => Connect();
    private void Disconnect_Click(object sender, RoutedEventArgs e) => _rpcService?.Disconnect();

    private void Connect()
    {
        ConnectBtn.IsEnabled = false;
        ConnectBtn.Content = Loc.Get("Connecting");

        SaveConfigFromUI();
        _config.Save();
        _rpcService?.UpdateConfig(_config);
        _rpcService?.Connect();
    }

    private void OnConnectionChanged(bool connected)
    {
        Dispatcher.Invoke(() =>
        {
            StatusDot.Fill = connected ? SuccessBrush : DangerBrush;
            StatusText.Foreground = connected ? SuccessBrush : TextSecondaryBrush;
            ConnectBtn.IsEnabled = !connected;
            ConnectBtn.Content = Loc.Get("Connect");
            DisconnectBtn.IsEnabled = connected;
            SetStatusText(connected);
        });
    }

    private void UpdateSystemInfo()
    {
        try
        {
            var info = _rpcService?.GetCurrentInfo();
            if (info == null) return;

            _sysInfo[0].Value = info.Cpu.Name;
            _sysInfo[1].Value = $"{info.Cpu.Load:F0}%";
            _sysInfo[2].Value = info.Cpu.Temperature.HasValue ? $"{info.Cpu.Temperature:F0}°C" : "—";
            _sysInfo[3].Value = info.Gpu.Name;
            _sysInfo[4].Value = $"{info.Gpu.Load:F0}%";
            _sysInfo[5].Value = info.Gpu.Temperature.HasValue ? $"{info.Gpu.Temperature:F0}°C" : "—";
            _sysInfo[6].Value = info.Ram.Name;
            _sysInfo[7].Value = $"{info.Ram.Used:F1} / {info.Ram.Total:F1} GB";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UpdateSystemInfo UI error: {ex.Message}");
        }
    }

    private void SetStatusText(bool connected)
    {
        StatusText.Text = connected ? Loc.Get("StatusConnected") : Loc.Get("StatusDisconnected");
    }

    private void ToggleSystem(object sender, MouseButtonEventArgs e)
    {
        _systemExpanded = SystemContent.Visibility == Visibility.Collapsed;
        SystemContent.Visibility = _systemExpanded ? Visibility.Visible : Visibility.Collapsed;
        SystemToggle.Text = _systemExpanded ? Loc.Get("SystemExpanded") : Loc.Get("System");
    }

    private void ToggleHelp(object sender, MouseButtonEventArgs e)
    {
        _helpExpanded = HelpContent.Visibility == Visibility.Collapsed;
        HelpContent.Visibility = _helpExpanded ? Visibility.Visible : Visibility.Collapsed;
        HelpToggle.Text = _helpExpanded ? Loc.Get("HelpExpanded") : Loc.Get("Help");
    }

    private void OpenDevPortal(object sender, MouseButtonEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://discord.com/developers/applications") { UseShellExecute = true });

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void OpenServer(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrEmpty(AppConstants.ProjectUrl)) return;
        Process.Start(new ProcessStartInfo(AppConstants.ProjectUrl) { UseShellExecute = true });
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_config) { Owner = this };
        if (settingsWindow.ShowDialog() == true)
            _rpcService?.UpdateConfig(_config);
    }

    private static int ActivityTypeToIndex(int type) => type switch
    {
        0 => 0,
        2 => 1,
        3 => 2,
        5 => 3,
        _ => 0
    };

    private static int IndexToActivityType(int index) =>
        index switch { 0 => 0, 1 => 2, 2 => 3, 3 => 5, _ => 0 };

    private static int MapWatchingTimerToIndex(string mode) => mode switch
    {
        "elapsed" => 0,
        "startOfDay" => 1,
        "progress" => 2,
        _ => 2
    };

    private static string MapIndexToWatchingTimer(int index) => index switch
    {
        0 => "elapsed",
        1 => "startOfDay",
        2 => "progress",
        _ => "progress"
    };

    private int MapImageSetToIndex(string imageSet) => imageSet switch
    {
        "kiara" => 0,
        "gura" => 1,
        "mori" => 2,
        "discord_label" => 3,
        "custom" => 4,
        _ => 0
    };

    private string MapIndexToImageSet(int index) => index switch
    {
        0 => "kiara",
        1 => "gura",
        2 => "mori",
        3 => "discord_label",
        4 => "custom",
        _ => "kiara"
    };
}
