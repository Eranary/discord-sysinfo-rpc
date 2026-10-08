using System.Windows;

namespace DiscordSysInfoRPC;

public partial class SettingsWindow : Window
{
    private readonly Config _config;

    public SettingsWindow(Config config)
    {
        InitializeComponent();
        _config = config;
        ApplyLocalization();
        LoadSettings();
    }

    private void ApplyLocalization()
    {
        Title = Loc.Get("SettingsTitle");
        TitleText.Text = Loc.Get("SettingsTitle");
        CustomImagesTitle.Text = Loc.Get("SettingsCustomImages");
        CustomImagesHint.Text = Loc.Get("SettingsCustomHint");
        IdleLabel.Text = Loc.Get("SettingsIdle");
        LowLabel.Text = Loc.Get("SettingsLow");
        MediumLabel.Text = Loc.Get("SettingsMedium");
        HighLabel.Text = Loc.Get("SettingsHigh");
        ThresholdsTitle.Text = Loc.Get("SettingsThresholds");
        ThresholdsHint.Text = Loc.Get("SettingsThresholdsHint");
        ThresholdLowLabel.Text = "Low";
        ThresholdMediumLabel.Text = "Medium";
        ThresholdHighLabel.Text = "High";
        ThresholdLegend.Text = Loc.Get("SettingsThresholdLegend");
        ResetBtn.Content = Loc.Get("Reset");
        SaveBtn.Content = Loc.Get("Save");
    }

    private void LoadSettings()
    {
        ImageIdleBox.Text = _config.CustomImageIdle;
        ImageLowBox.Text = _config.CustomImageLow;
        ImageMediumBox.Text = _config.CustomImageMedium;
        ImageHighBox.Text = _config.CustomImageHigh;
        ThresholdLowBox.Text = _config.ThresholdLow.ToString();
        ThresholdMediumBox.Text = _config.ThresholdMedium.ToString();
        ThresholdHighBox.Text = _config.ThresholdHigh.ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _config.CustomImageIdle = ImageIdleBox.Text.Trim();
        _config.CustomImageLow = ImageLowBox.Text.Trim();
        _config.CustomImageMedium = ImageMediumBox.Text.Trim();
        _config.CustomImageHigh = ImageHighBox.Text.Trim();

        if (int.TryParse(ThresholdLowBox.Text, out var low))
            _config.ThresholdLow = Math.Clamp(low, 1, 99);
        if (int.TryParse(ThresholdMediumBox.Text, out var med))
            _config.ThresholdMedium = Math.Clamp(med, 1, 99);
        if (int.TryParse(ThresholdHighBox.Text, out var high))
            _config.ThresholdHigh = Math.Clamp(high, 1, 99);

        _config.Save();
        DialogResult = true;
        Close();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ImageIdleBox.Text = "";
        ImageLowBox.Text = "";
        ImageMediumBox.Text = "";
        ImageHighBox.Text = "";
        ThresholdLowBox.Text = "35";
        ThresholdMediumBox.Text = "65";
        ThresholdHighBox.Text = "75";
    }
}
