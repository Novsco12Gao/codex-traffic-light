using System.Text.Json;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

/// <summary>
/// 覆盖 hook 安装行为和生成脚本内容。
/// 重点保护“不覆盖用户已有 hooks”和“生成脚本包含会话、诊断、自动启动能力”。
/// </summary>
public sealed class CodexHookInstallerTests
{
    [Fact]
    public void InstallPreservesUnrelatedHooks()
    {
        var root = CreateTempRoot();
        var paths = new CodexPaths(root);
        paths.EnsureCodexDirectory();
        File.WriteAllText(paths.HooksPath, """
        {
          "hooks": {
            "PreToolUse": [
              {
                "matcher": "Bash",
                "hooks": [
                  {
                    "type": "command",
                    "command": "echo keep-me"
                  }
                ]
              }
            ]
          }
        }
        """);

        new CodexHookInstaller(paths).InstallOrUpdate();
        var json = File.ReadAllText(paths.HooksPath);

        Assert.Contains("echo keep-me", json);
        Assert.Contains("codex_traffic_light_write_status.ps1", json);
        Assert.Contains("UserPromptSubmit", json);
        Assert.Contains("PermissionRequest", json);
        Assert.Contains("Stop", json);
        Assert.Contains("SessionStart", json);
        Assert.True(File.Exists(paths.HookScriptPath));
    }

    [Fact]
    public void InstallIsIdempotent()
    {
        var root = CreateTempRoot();
        var paths = new CodexPaths(root);
        var installer = new CodexHookInstaller(paths);

        installer.InstallOrUpdate();
        var first = File.ReadAllText(paths.HooksPath);
        installer.InstallOrUpdate();
        var second = File.ReadAllText(paths.HooksPath);

        Assert.Equal(NormalizeJson(first), NormalizeJson(second));
    }

    [Fact]
    public void InvalidHooksJsonIsBackedUp()
    {
        var root = CreateTempRoot();
        var paths = new CodexPaths(root);
        paths.EnsureCodexDirectory();
        File.WriteAllText(paths.HooksPath, "{bad-json");

        new CodexHookInstaller(paths).InstallOrUpdate();

        Assert.NotEmpty(Directory.GetFiles(paths.CodexDirectory, "hooks.json.invalid-*.bak"));
        Assert.Contains("codex_traffic_light_write_status.ps1", File.ReadAllText(paths.HooksPath));
    }

    [Fact]
    public void HookScriptWritesDiagnosticsAndPerSessionStatusFiles()
    {
        var root = CreateTempRoot();
        var paths = new CodexPaths(root);

        new CodexHookInstaller(paths).InstallOrUpdate();

        var script = File.ReadAllText(paths.HookScriptPath);
        Assert.Contains("diagnostics", script);
        Assert.Contains("Get-CimInstance Win32_Process", script);
        Assert.Contains("sessions", script);
        Assert.Contains(".json.tmp", script);
        Assert.Contains("Move-Item", script);
        Assert.Contains("CODEX_TRAFFIC_LIGHT_SESSION_ID", script);
        Assert.Contains("[Console]::In.ReadToEnd()", script);
        Assert.Contains("[Console]::InputEncoding", script);
        Assert.Contains("TrimStart([char]0xFEFF)", script);
        Assert.Contains("session_id", script);
        Assert.Contains("prompt", script);
        Assert.Contains("rawHookInput", script);
    }

    [Fact]
    public void HookScriptCanAutoLaunchAppWhenCodexActivityStarts()
    {
        var root = CreateTempRoot();
        var paths = new CodexPaths(root);

        new CodexHookInstaller(paths).InstallOrUpdate();

        var script = File.ReadAllText(paths.HookScriptPath);
        Assert.Contains("AutoLaunchOnCodexActivity", script);
        Assert.Contains(paths.SettingsPath, script);
        Assert.Contains("CodexTrafficLight.App.exe", script);
        Assert.Contains("Start-Process", script);
    }

    private static string NormalizeJson(string json)
    {
        // 格式无关紧要；通过规范化 JSON 比较幂等性。
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc.RootElement);
    }

    private static string CreateTempRoot()
    {
        // 将 hook 写入隔离开，避免影响开发者真实 Codex 配置。
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
