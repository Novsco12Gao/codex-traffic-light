namespace CodexTrafficLight.Core.Models;

/// <summary>
/// 更新检查器从远程 version.json 读取的发布清单。
/// 该清单只负责提示用户有新版本，不会自动下载或替换程序。
/// </summary>
public sealed record UpdateManifest
{
    /// <summary>
    /// 远程发布版本号。
    /// 当前只接受两段或三段数字版本，例如 1.0 或 1.0.3。
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// 更新提示中显示的发布标题。
    /// 为空时会由更新检查器使用版本号生成默认标题。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 发布说明列表。
    /// 界面会逐条显示这些内容，方便用户判断是否需要下载新版本。
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 用户下载或查看该发布版本的地址。
    /// 为了避免不安全跳转，更新检查器只接受 HTTPS 地址。
    /// </summary>
    public string DownloadUrl { get; init; } = string.Empty;
}
