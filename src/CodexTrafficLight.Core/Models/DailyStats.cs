namespace CodexTrafficLight.Core.Models;

/// <summary>
/// 单个本地日历日的持久化计数。
/// </summary>
public sealed record DailyStats
{
    /// <summary>
    /// Codex 进入工作状态的次数。
    /// </summary>
    public int RedCount { get; init; }

    /// <summary>
    /// Codex 到达完成状态的次数。
    /// </summary>
    public int GreenCount { get; init; }

    /// <summary>
    /// 当天处于工作状态的总时长。
    /// </summary>
    public long RedDurationMs { get; init; }
}
