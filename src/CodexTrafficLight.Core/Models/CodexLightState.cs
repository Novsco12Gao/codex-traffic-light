namespace CodexTrafficLight.Core.Models;

/// <summary>
/// hook 脚本、持久化文件和界面共用的状态颜色。
/// </summary>
public enum CodexLightState
{
    /// <summary>
    /// 尚未写入可用状态。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Codex 正在工作。
    /// </summary>
    Red,

    /// <summary>
    /// Codex 正在等待用户输入。
    /// </summary>
    Yellow,

    /// <summary>
    /// Codex 已完成当前轮次。
    /// </summary>
    Green
}
