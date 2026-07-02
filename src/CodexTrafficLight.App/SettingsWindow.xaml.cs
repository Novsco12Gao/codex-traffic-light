using System.Windows;
using System.Windows.Controls;
using CodexTrafficLight.Core.Models;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace CodexTrafficLight.App;

/// <summary>
/// 托盘设置窗口。
/// 窗口打开时接收一份设置副本，只有用户点击保存后才把新设置交回主窗口。
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>
    /// 根据当前设置初始化所有控件状态，保证用户看到的是实际生效的配置。
    /// </summary>
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

    /// <summary>
    /// 用户保存后的设置结果。
    /// 对话框取消时主窗口不会读取这个值。
    /// </summary>
    public AppSettings Settings { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // record 的 with 表达式可以保留未显示在窗口里的设置字段，只替换本窗口负责的选项。
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
        // ComboBoxItem 的 Tag 保存真实配置值，Content 只负责展示中文。
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        // 找不到旧值时回到第一项，避免设置文件被手动改坏后窗口没有选中项。
        comboBox.SelectedIndex = 0;
    }

    private static string GetSelectedTag(WpfComboBox comboBox, string fallback)
    {
        // 保存时只写入 Tag 中的稳定值，不写入会随界面文案变化的中文文本。
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;
    }

    private static int ReadPositiveInt(WpfTextBox textBox, int fallback)
    {
        // 时间设置只接受正整数；输入为空或无效时保留原值，避免误写成零。
        return int.TryParse(textBox.Text, out var value) && value > 0 ? value : fallback;
    }
}
