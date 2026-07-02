using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

/// <summary>
/// 验证显式测试主目录和 CODEX_HOME 的路径解析。
/// 路径解析正确时，应用才不会误写到错误的 Codex 配置目录。
/// </summary>
public sealed class CodexPathsTests
{
    [Fact]
    public void ExplicitHomeDirectoryUsesLocalCodexDirectory()
    {
        var root = CreateTempRoot();

        var paths = new CodexPaths(root);

        Assert.Equal(Path.Combine(root, ".codex"), paths.CodexDirectory);
        Assert.Equal(Path.Combine(root, ".codex", "hooks.json"), paths.HooksPath);
    }

    [Fact]
    public void EnvironmentCodexHomeOverridesDefaultHome()
    {
        var codexHome = CreateTempRoot();
        var previous = Environment.GetEnvironmentVariable("CODEX_HOME");

        try
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
            var paths = new CodexPaths();

            Assert.Equal(codexHome, paths.CodexDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", previous);
        }
    }

    private static string CreateTempRoot()
    {
        // 环境变量覆盖测试需要真实目录，而不只是随机字符串。
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
