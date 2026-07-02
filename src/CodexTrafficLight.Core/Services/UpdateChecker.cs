using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 下载远程发布清单，并与当前运行的应用版本进行比较。
/// 检查结果只用于提示用户，不会自动安装或修改本地程序。
/// </summary>
public sealed class UpdateChecker
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create();
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 注入 HTTP 客户端，方便生产环境复用网络配置，也方便测试提供假响应。
    /// </summary>
    public UpdateChecker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// 使用 HTTPS 清单地址和调用方提供的超时时间检查更新。
    /// 所有网络、解析和版本校验失败都会转换为可显示的失败结果。
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        string manifestUrl,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseVersion(currentVersion, out var current))
        {
            return Fail("当前版本号无效。", currentVersion);
        }

        // 不允许非 HTTPS 清单，因为它控制更新提示和下载链接。
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var manifestUri) ||
            manifestUri.Scheme != Uri.UriSchemeHttps)
        {
            return Fail("更新地址未配置或不是 HTTPS。", currentVersion);
        }

        try
        {
            // 将调用方取消令牌和本地超时关联起来，保证界面不会因为网络阻塞而长期卡住。
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            using var response = await _httpClient.GetAsync(manifestUri, timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Fail("暂时无法检查更新。", currentVersion);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOptions, timeoutSource.Token);
            if (manifest is null)
            {
                return Fail("更新信息格式无效。", currentVersion);
            }

            return BuildResult(currentVersion, current, manifest);
        }
        catch
        {
            return Fail("暂时无法检查更新。", currentVersion);
        }
    }

    private static UpdateCheckResult BuildResult(string currentVersion, Version current, UpdateManifest manifest)
    {
        // 远程版本号无效时不能继续比较，否则用户会看到误导性的更新提示。
        if (!TryParseVersion(manifest.Version, out var latest))
        {
            return Fail("远程版本号无效。", currentVersion);
        }

        // 清单可能通过 HTTPS 获取，但仍可能包含不安全的下载地址。
        if (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out var downloadUri) ||
            downloadUri.Scheme != Uri.UriSchemeHttps)
        {
            return Fail("下载地址无效。", currentVersion);
        }

        var hasUpdate = latest > current;
        return new UpdateCheckResult
        {
            IsSuccess = true,
            HasUpdate = hasUpdate,
            CurrentVersion = currentVersion,
            LatestVersion = manifest.Version,
            Title = string.IsNullOrWhiteSpace(manifest.Title)
                ? $"Codex 红绿灯 {manifest.Version}"
                : manifest.Title.Trim(),
            Notes = manifest.Notes
                .Where(note => !string.IsNullOrWhiteSpace(note))
                .Select(note => note.Trim())
                .Take(8)
                .ToArray(),
            DownloadUrl = manifest.DownloadUrl,
            Message = hasUpdate ? "发现新版本。" : "当前已是最新版本。"
        };
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        version = new Version();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // 接受两段或三段数字版本号，保持发布清单简单。
        var parts = value.Trim().Split('.');
        if (parts.Length is < 2 or > 3)
        {
            return false;
        }

        if (parts.Any(part => part.Length == 0 || part.Any(ch => !char.IsDigit(ch))))
        {
            return false;
        }

        return Version.TryParse(string.Join('.', parts), out version!);
    }

    private static UpdateCheckResult Fail(string message, string currentVersion)
    {
        // 失败结果保留当前版本，方便界面提示时仍能告诉用户正在运行的版本。
        return new UpdateCheckResult
        {
            IsSuccess = false,
            HasUpdate = false,
            CurrentVersion = currentVersion,
            Message = message
        };
    }
}
