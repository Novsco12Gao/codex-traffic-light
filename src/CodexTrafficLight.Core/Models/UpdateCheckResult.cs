namespace CodexTrafficLight.Core.Models;

/// <summary>
/// 更新检查器比较本地版本和远程清单后返回的结果。
/// 主窗口只读取这个对象，不直接关心网络请求或清单解析细节。
/// </summary>
public sealed record UpdateCheckResult
{
    /// <summary>
    /// 表示远程清单是否成功加载并解析。
    /// 为 false 时，界面会直接显示 Message 中的错误说明。
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 表示远程版本是否比当前运行版本更新。
    /// 只有 IsSuccess 为 true 时，这个值才代表有效比较结果。
    /// </summary>
    public bool HasUpdate { get; init; }

    /// <summary>
    /// 当前机器正在运行的应用版本。
    /// 该值来自程序程序集版本，用于和远程 Version 比较。
    /// </summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>
    /// 远程清单声明的最新版本。
    /// 当 HasUpdate 为 true 时，界面会把它显示为可下载的新版本。
    /// </summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>
    /// 有可用更新时展示给用户的发布标题。
    /// 为空清单标题会被替换为包含版本号的默认标题。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 更新清单中的发布说明。
    /// 更新检查器会过滤空白项，并限制显示数量，避免弹窗过长。
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 最新版本的安装包或发布页地址。
    /// 用户确认后，主窗口会用系统默认浏览器打开该地址。
    /// </summary>
    public string DownloadUrl { get; init; } = string.Empty;

    /// <summary>
    /// 用于界面展示的可读错误或状态消息。
    /// 成功时表示发现新版本或当前已是最新；失败时表示失败原因。
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
