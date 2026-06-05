namespace CodexTrafficLight.Core.Models;

/// <summary>
/// 更新检查器比较本地和远程版本后返回的结果。
/// </summary>
public sealed record UpdateCheckResult
{
    /// <summary>
    /// 表示远程清单是否成功加载并解析。
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 表示远程版本是否比当前运行版本更新。
    /// </summary>
    public bool HasUpdate { get; init; }

    /// <summary>
    /// 当前机器正在运行的版本。
    /// </summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>
    /// 更新清单声明的最新版本。
    /// </summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>
    /// 有可用更新时展示给用户的发布标题。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 更新清单中的发布说明。
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 最新版本的安装包或发布页地址。
    /// </summary>
    public string DownloadUrl { get; init; } = string.Empty;

    /// <summary>
    /// 用于界面展示的可读错误或状态消息。
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
