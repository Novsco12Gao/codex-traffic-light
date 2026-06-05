using System.Windows;
using System.Windows.Controls;
using CodexTrafficLight.Core.Models;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace CodexTrafficLight.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Settings = settings;

        SelectByTag(StyleComboBox, settings.Style);
        SelectByTag(ThemeComboBox, settings.Theme);
        SelectByTag(LampEffectComboBox, settings.LampEffect);
        SelectByTag(LampSpeedComboBox, settings.LampSpeed);
        SelectByTag(ReminderModeComboBox, settings.ReminderMode);
        TopmostCheckBox.IsChecked = settings.Topmost;
        AutoOpenDrawerCheckBox.IsChecked = settings.AutoOpenDrawerOnYellow;
        AutoLaunchCheckBox.IsChecked = settings.AutoLaunchOnCodexActivity;
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
        ShowEndedSessionsCheckBox.IsChecked = settings.ShowEndedSessions;
        MutedCheckBox.IsChecked = settings.Muted;
        GreenRetentionTextBox.Text = settings.GreenRetentionMinutes.ToString();
        YellowRetentionTextBox.Text = settings.YellowRetentionMinutes.ToString();
        RedRetentionTextBox.Text = settings.RedRetentionMinutes.ToString();
        LiveCliRetentionTextBox.Text = settings.LiveCliWorkRetentionHours.ToString();
        LiveVsCodePluginRetentionTextBox.Text = settings.LiveVsCodePluginWorkRetentionHours.ToString();
    }

    public AppSettings Settings { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Settings = Settings with
        {
            Style = GetSelectedTag(StyleComboBox, Settings.Style),
            Theme = GetSelectedTag(ThemeComboBox, Settings.Theme),
            LampEffect = GetSelectedTag(LampEffectComboBox, Settings.LampEffect),
            LampSpeed = GetSelectedTag(LampSpeedComboBox, Settings.LampSpeed),
            ReminderMode = GetSelectedTag(ReminderModeComboBox, Settings.ReminderMode),
            Topmost = TopmostCheckBox.IsChecked == true,
            AutoOpenDrawerOnYellow = AutoOpenDrawerCheckBox.IsChecked == true,
            AutoLaunchOnCodexActivity = AutoLaunchCheckBox.IsChecked == true,
            StartWithWindows = StartWithWindowsCheckBox.IsChecked == true,
            ShowEndedSessions = ShowEndedSessionsCheckBox.IsChecked == true,
            Muted = MutedCheckBox.IsChecked == true,
            GreenRetentionMinutes = ReadPositiveInt(GreenRetentionTextBox, Settings.GreenRetentionMinutes),
            YellowRetentionMinutes = ReadPositiveInt(YellowRetentionTextBox, Settings.YellowRetentionMinutes),
            RedRetentionMinutes = ReadPositiveInt(RedRetentionTextBox, Settings.RedRetentionMinutes),
            LiveCliWorkRetentionHours = ReadPositiveInt(LiveCliRetentionTextBox, Settings.LiveCliWorkRetentionHours),
            LiveVsCodePluginWorkRetentionHours = ReadPositiveInt(LiveVsCodePluginRetentionTextBox, Settings.LiveVsCodePluginWorkRetentionHours)
        };

        DialogResult = true;
    }

    private static void SelectByTag(WpfComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static string GetSelectedTag(WpfComboBox comboBox, string fallback)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;
    }

    private static int ReadPositiveInt(WpfTextBox textBox, int fallback)
    {
        return int.TryParse(textBox.Text, out var value) && value > 0 ? value : fallback;
    }
}
