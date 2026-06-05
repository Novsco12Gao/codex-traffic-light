using System.Windows;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.App;

/// <summary>
/// 显示预先计算好的每日和每周 Codex 活动摘要。
/// </summary>
public partial class StatsWindow : Window
{
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
        redCount.Text = $"思考次数：{summary.RedCount} 次";
        greenCount.Text = $"完成次数：{summary.GreenCount} 次";
        duration.Text = $"思考总时长：{FormatDuration(summary.RedDurationMs)}";
        average.Text = $"{label}平均：{FormatDuration(summary.AverageRedDurationMs)}";
    }

    private static string FormatDuration(long milliseconds)
    {
        // 小型统计对话框使用紧凑显示即可。
        var time = TimeSpan.FromMilliseconds(milliseconds);
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours} 小时 {time.Minutes} 分钟";
        }

        return $"{time.Minutes} 分钟 {time.Seconds} 秒";
    }
}
