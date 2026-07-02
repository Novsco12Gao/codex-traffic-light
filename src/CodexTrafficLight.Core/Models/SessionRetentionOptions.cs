namespace CodexTrafficLight.Core.Models;

/// <summary>
/// 控制会话快照在任务抽屉中保留多久的策略。
/// 这些值来自用户设置，默认值保持当前应用的原始隐藏规则。
/// </summary>
public sealed record SessionRetentionOptions
{
    /// <summary>
    /// 绿灯会话在完成后继续显示的时间。
    /// 超过该时间后，默认会从任务抽屉中隐藏。
    /// </summary>
    public TimeSpan GreenRetention { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 黄灯会话在没有新 hook 更新后继续显示的时间。
    /// 如果关联进程仍在运行，还会再按来源类型应用运行中保留时间。
    /// </summary>
    public TimeSpan YellowRetention { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 红灯会话在没有新 hook 更新后继续显示的时间。
    /// 该值用于避免已经卡住或失联的旧任务长期占据主灯状态。
    /// </summary>
    public TimeSpan RedRetention { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Codex CLI 进程仍然存活时，红灯或黄灯会话最多继续保留的时间。
    /// CLI 任务可能运行很久，因此默认时间比 VS Code 插件更长。
    /// </summary>
    public TimeSpan LiveCliWorkRetention { get; init; } = TimeSpan.FromHours(6);

    /// <summary>
    /// VS Code 插件会话对应进程仍然存活时，红灯或黄灯会话最多继续保留的时间。
    /// 插件会话通常刷新更频繁，所以默认保留时间更短。
    /// </summary>
    public TimeSpan LiveVsCodePluginWorkRetention { get; init; } = TimeSpan.FromHours(2);
}
