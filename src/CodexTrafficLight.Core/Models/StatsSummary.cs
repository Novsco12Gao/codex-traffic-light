namespace CodexTrafficLight.Core.Models;

/// <summary>
/// 根据每日历史记录计算出的聚合统计。
/// 统计窗口会用它同时展示“今天”和“本周”的汇总结果。
/// </summary>
public sealed record StatsSummary
{
    /// <summary>
    /// 统计摘要中包含的红灯次数。
    /// 每次 Codex 从非红灯状态进入红灯状态时，都会记为一次工作开始。
    /// </summary>
    public int RedCount { get; init; }

    /// <summary>
    /// 统计摘要中包含的绿灯次数。
    /// 每次 Codex 从其它状态进入绿灯状态时，都会记为一次完成。
    /// </summary>
    public int GreenCount { get; init; }

    /// <summary>
    /// 统计时间窗口内红灯状态的总时长，单位为毫秒。
    /// 该值由红灯开始时间和下一次状态变化时间计算得出。
    /// </summary>
    public long RedDurationMs { get; init; }

    /// <summary>
    /// 平均每次红灯工作的时长，单位为毫秒。
    /// 没有红灯记录时返回零，避免统计窗口出现除零错误。
    /// </summary>
    public long AverageRedDurationMs => RedCount == 0 ? 0 : RedDurationMs / RedCount;
}
