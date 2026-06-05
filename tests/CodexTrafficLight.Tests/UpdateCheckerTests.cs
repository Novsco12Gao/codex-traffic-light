using System.Net;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

/// <summary>
/// 使用内存 HTTP 处理器验证更新清单比较。
/// </summary>
public sealed class UpdateCheckerTests
{
    [Fact]
    public async Task CheckAsyncReturnsAvailableUpdateWhenRemoteVersionIsNewer()
    {
        using var http = CreateHttpClient("""
        {
          "version": "1.0.1",
          "title": "Codex 红绿灯 1.0.1",
          "notes": ["优化多任务识别", "修复浅色模式显示"],
          "downloadUrl": "https://github.com/gyk/codex-traffic-light/releases/tag/v1.0.1"
        }
        """);
        var checker = new UpdateChecker(http);

        var result = await checker.CheckAsync("1.0.0", "https://example.com/version.json", TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.True(result.HasUpdate);
        Assert.Equal("1.0.1", result.LatestVersion);
        Assert.Equal("https://github.com/gyk/codex-traffic-light/releases/tag/v1.0.1", result.DownloadUrl);
        Assert.Contains("优化多任务识别", result.Notes);
    }

    [Fact]
    public async Task CheckAsyncReturnsNoUpdateWhenRemoteVersionIsSame()
    {
        using var http = CreateHttpClient("""
        {
          "version": "1.0.0",
          "downloadUrl": "https://github.com/gyk/codex-traffic-light/releases/tag/v1.0.0"
        }
        """);
        var checker = new UpdateChecker(http);

        var result = await checker.CheckAsync("1.0.0", "https://example.com/version.json", TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.False(result.HasUpdate);
    }

    [Theory]
    [InlineData("""{"version":"latest","downloadUrl":"https://github.com/gyk/codex-traffic-light/releases"}""")]
    [InlineData("""{"version":"1.0.1","downloadUrl":"javascript:alert(1)"}""")]
    public async Task CheckAsyncRejectsUnsafeManifestValues(string json)
    {
        using var http = CreateHttpClient(json);
        var checker = new UpdateChecker(http);

        var result = await checker.CheckAsync("1.0.0", "https://example.com/version.json", TimeSpan.FromSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.False(result.HasUpdate);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public async Task CheckAsyncFailsQuietlyWhenRemoteRequestFails()
    {
        using var http = CreateHttpClient("{}", HttpStatusCode.NotFound);
        var checker = new UpdateChecker(http);

        var result = await checker.CheckAsync("1.0.0", "https://example.com/version.json", TimeSpan.FromSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.False(result.HasUpdate);
        Assert.Equal("暂时无法检查更新。", result.Message);
    }

    private static HttpClient CreateHttpClient(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        // 避免真实网络调用，同时保留 HttpClient 行为。
        return new HttpClient(new StaticResponseHandler(responseBody, statusCode));
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;

        public StaticResponseHandler(string responseBody, HttpStatusCode statusCode)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // 无论 URL 如何都返回配置好的响应；URL 校验在其他位置测试。
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody)
            };

            return Task.FromResult(response);
        }
    }
}
