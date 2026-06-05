namespace CodexTrafficLight.Core.Models;

/// <summary>
/// 更新检查器读取的远程发布清单。
/// </summary>
public sealed record UpdateManifest
{
    /// <summary>
    /// 声明发布版本的语义化版本字符串。
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// 更新提示中显示的简短发布标题。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 声明版本的项目符号式发布说明。
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 用户下载或查看该发布版本的地址。
    /// </summary>
    public string DownloadUrl { get; init; } = string.Empty;
}
