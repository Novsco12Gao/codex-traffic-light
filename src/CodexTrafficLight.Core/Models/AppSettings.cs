namespace CodexTrafficLight.Core.Models;

/// <summary>
/// 保存在 Codex 主目录下的用户可调整选项。
/// </summary>
public sealed record AppSettings
{
    /// <summary>
    /// 主窗口上次保存的水平位置。
    /// </summary>
    public double? WindowLeft { get; init; }

    /// <summary>
    /// 主窗口上次保存的垂直位置。
    /// </summary>
    public double? WindowTop { get; init; }

    /// <summary>
    /// WPF 外壳使用的视觉主题名称。
    /// </summary>
    public string Theme { get; init; } = "dark";

    /// <summary>
    /// 用户选择的灯组布局样式。
    /// </summary>
    public string Style { get; init; } = "triple";

    /// <summary>
    /// 启用后静音提示通知。
    /// </summary>
    public bool Muted { get; init; }

    /// <summary>
    /// 当 Codex 等待输入时自动打开会话抽屉。
    /// </summary>
    public bool AutoOpenDrawerOnYellow { get; init; } = true;

    /// <summary>
    /// 检测到 hook 活动时自动启动应用。
    /// </summary>
    public bool AutoLaunchOnCodexActivity { get; init; } = true;

    /// <summary>
    /// 在抽屉中保留已完成会话，而不是过滤掉它们。
    /// </summary>
    public bool ShowEndedSessions { get; init; }

    public bool StartWithWindows { get; init; }

    public string ReminderMode { get; init; } = "balloon";

    public int GreenRetentionMinutes { get; init; } = 5;

    public int YellowRetentionMinutes { get; init; } = 5;

    public int RedRetentionMinutes { get; init; } = 10;

    public int LiveCliWorkRetentionHours { get; init; } = 6;

    public int LiveVsCodePluginWorkRetentionHours { get; init; } = 2;

    /// <summary>
    /// 让红绿灯窗口保持在普通应用窗口之上。
    /// </summary>
    public bool Topmost { get; init; } = true;

    /// <summary>
    /// 应用到当前亮灯的动画效果。
    /// </summary>
    public string LampEffect { get; init; } = "breath";

    /// <summary>
    /// 灯效动画的速度预设。
    /// </summary>
    public string LampSpeed { get; init; } = "standard";

    public SessionRetentionOptions ToSessionRetentionOptions()
    {
        return new SessionRetentionOptions
        {
            GreenRetention = TimeSpan.FromMinutes(UsePositiveOrDefault(GreenRetentionMinutes, 5)),
            YellowRetention = TimeSpan.FromMinutes(UsePositiveOrDefault(YellowRetentionMinutes, 5)),
            RedRetention = TimeSpan.FromMinutes(UsePositiveOrDefault(RedRetentionMinutes, 10)),
            LiveCliWorkRetention = TimeSpan.FromHours(UsePositiveOrDefault(LiveCliWorkRetentionHours, 6)),
            LiveVsCodePluginWorkRetention = TimeSpan.FromHours(UsePositiveOrDefault(LiveVsCodePluginWorkRetentionHours, 2))
        };
    }

    private static int UsePositiveOrDefault(int value, int fallback)
    {
        return value > 0 ? value : fallback;
    }
}
