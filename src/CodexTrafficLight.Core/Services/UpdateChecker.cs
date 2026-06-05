using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 下载发布清单并与当前运行的应用版本比较。
/// </summary>
public sealed class UpdateChecker
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create();
    private readonly HttpClient _httpClient;

    public UpdateChecker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// 使用 HTTPS 清单地址和调用方提供的超时时间检查更新。
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

        // 不允许非 HTTPS 清单，因为它控制更新提示和链接。
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var manifestUri) ||
            manifestUri.Scheme != Uri.UriSchemeHttps)
        {
            return Fail("更新地址未配置或不是 HTTPS。", currentVersion);
        }

        try
        {
            // 将调用方取消令牌和本地超时关联起来，保证界面行为可预测。
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
        return new UpdateCheckResult
        {
            IsSuccess = false,
            HasUpdate = false,
            CurrentVersion = currentVersion,
            Message = message
        };
    }
}
