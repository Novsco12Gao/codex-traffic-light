namespace CodexTrafficLight.Core.Models;

/// <summary>
/// 单个 Codex 会话文件的状态快照。
/// </summary>
public sealed record CodexSessionStatus
{
    /// <summary>
    /// 作为会话文件名和界面键值使用的稳定标识。
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// 显示在会话抽屉中的易读标签。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 运行中的 Codex 进程报告的工作区目录。
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// 状态更新来源，例如 hook 或进程清理。
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// 该会话当前的红绿灯颜色。
    /// </summary>
    public CodexLightState State { get; init; } = CodexLightState.Unknown;

    /// <summary>
    /// 最近一次改变会话状态的原始生命周期事件。
    /// </summary>
    public string Event { get; init; } = "unknown";

    /// <summary>
    /// hook 能识别时，与该会话关联的进程 ID。
    /// </summary>
    public int ProcessId { get; init; }

    /// <summary>
    /// 从 Codex 进程捕获的启动时间，用于避免 PID 复用误判。
    /// </summary>
    public DateTimeOffset? ProcessStartTime { get; init; }

    /// <summary>
    /// 该会话快照最近一次写入的时间戳。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
}
