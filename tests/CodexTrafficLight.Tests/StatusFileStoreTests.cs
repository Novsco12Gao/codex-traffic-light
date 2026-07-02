using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

/// <summary>
/// 验证聚合状态文件的往返读写和兜底状态。
/// 这些测试确保状态文件缺失或损坏时，主程序仍能安全启动。
/// </summary>
public sealed class StatusFileStoreTests
{
    [Fact]
    public void WriteAndReadStatusRoundTrips()
    {
        var root = CreateTempRoot();
        var store = new StatusFileStore(new CodexPaths(root));
        var status = new CodexStatus(
            CodexLightState.Red,
            "UserPromptSubmit",
            DateTimeOffset.Parse("2026-06-01T10:45:00+08:00"));

        store.Write(status);
        var actual = store.Read();

        Assert.Equal(CodexLightState.Red, actual.State);
        Assert.Equal("UserPromptSubmit", actual.Event);
        Assert.Equal(status.UpdatedAt, actual.UpdatedAt);
    }

    [Fact]
    public void ReadReturnsUnknownWhenFileIsMissing()
    {
        var root = CreateTempRoot();
        var store = new StatusFileStore(new CodexPaths(root));

        var actual = store.Read();

        Assert.Equal(CodexLightState.Unknown, actual.State);
        Assert.Equal("missing", actual.Event);
    }

    [Fact]
    public void ReadReturnsUnknownWhenJsonIsInvalid()
    {
        var root = CreateTempRoot();
        var paths = new CodexPaths(root);
        paths.EnsureCodexDirectory();
        File.WriteAllText(paths.StatusPath, "not-json");
        var store = new StatusFileStore(paths);

        var actual = store.Read();

        Assert.Equal(CodexLightState.Unknown, actual.State);
        Assert.Equal("error", actual.Event);
    }

    private static string CreateTempRoot()
    {
        // 状态文件与用户真实 CODEX_HOME 隔离。
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
