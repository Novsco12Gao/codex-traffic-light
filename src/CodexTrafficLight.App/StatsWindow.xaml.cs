using System.Windows;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.App;

/// <summary>
/// 显示预先计算好的今日和本周 Codex 活动摘要。
/// 窗口只负责展示，不直接读取或修改统计文件。
/// </summary>
public partial class StatsWindow : Window
{
    /// <summary>
    /// 接收已经计算好的统计结果，并分别填充今日和本周两组文本。
    /// </summary>
    public StatsWindow(StatsSummary today, StatsSummary week)
    {
        InitializeComponent();
        ApplySummary("今日", today, TodayRedCountText, TodayGreenCountText, TodayDurationText, TodayAverageText);
        ApplySummary("本周", week, WeekRedCountText, WeekGreenCountText, WeekDurationText, WeekAverageText);
    }

    private static void ApplySummary(
        string label,
        StatsSummary summary,
        System.Windows.Controls.TextBlock redCount,
        System.Windows.Controls.TextBlock greenCount,
        System.Windows.Controls.TextBlock duration,
        System.Windows.Controls.TextBlock average)
    {
        // 保持四个指标的文案结构一致，便于用户快速对比今日和本周。
        redCount.Text = $"思考次数：{summary.RedCount} 次";
        greenCount.Text = $"完成次数：{summary.GreenCount} 次";
        duration.Text = $"思考总时长：{FormatDuration(summary.RedDurationMs)}";
        average.Text = $"{label}平均：{FormatDuration(summary.AverageRedDurationMs)}";
    }

    private static string FormatDuration(long milliseconds)
    {
        // 小型统计对话框使用紧凑显示即可，不展示毫秒级细节。
        var time = TimeSpan.FromMilliseconds(milliseconds);
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours} 小时 {time.Minutes} 分钟";
        }

        return $"{time.Minutes} 分钟 {time.Seconds} 秒";
    }
}
