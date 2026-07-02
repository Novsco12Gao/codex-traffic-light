namespace CodexTrafficLight.Core.Models;

/// <summary>
/// 单个本地日历日的持久化统计记录。
/// 该对象会按日期写入统计文件，用于之后汇总今天和本周的数据。
/// </summary>
public sealed record DailyStats
{
    /// <summary>
    /// Codex 进入红灯工作状态的次数。
    /// 只有状态真正发生变化时才会增加，重复写入同一状态不会重复计数。
    /// </summary>
    public int RedCount { get; init; }

    /// <summary>
    /// Codex 进入绿灯完成状态的次数。
    /// 这个值用于统计一天内完成了多少轮 Codex 工作。
    /// </summary>
    public int GreenCount { get; init; }

    /// <summary>
    /// 当天处于红灯工作状态的总时长，单位为毫秒。
    /// 它由红灯开始时间到下一次状态变化之间的间隔累加得到。
    /// </summary>
    public long RedDurationMs { get; init; }
}
