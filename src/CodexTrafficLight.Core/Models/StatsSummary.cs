namespace CodexTrafficLight.Core.Models;

/// <summary>
/// 根据每日历史记录计算出的聚合统计。
/// </summary>
public sealed record StatsSummary
{
    /// <summary>
    /// 统计摘要中包含的工作状态总次数。
    /// </summary>
    public int RedCount { get; init; }

    /// <summary>
    /// 统计摘要中包含的完成状态总次数。
    /// </summary>
    public int GreenCount { get; init; }

    /// <summary>
    /// 统计时间窗口内工作状态的总时长。
    /// </summary>
    public long RedDurationMs { get; init; }

    /// <summary>
    /// 平均工作状态时长；没有红灯记录时为零。
    /// </summary>
    public long AverageRedDurationMs => RedCount == 0 ? 0 : RedDurationMs / RedCount;
}
