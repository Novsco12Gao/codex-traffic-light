namespace CodexTrafficLight.Core.Models;

/// <summary>
/// Codex hooks 写入的当前聚合红绿灯状态。
/// </summary>
/// <param name="State">当前生效的红绿灯颜色。</param>
/// <param name="Event">产生该状态的原始 hook 事件。</param>
/// <param name="UpdatedAt">状态文件最后更新的时间。</param>
public sealed record CodexStatus(
    CodexLightState State,
    string Event,
    DateTimeOffset UpdatedAt);
