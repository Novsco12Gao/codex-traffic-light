using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

/// <summary>
/// 验证会话的可见性、保留时间、分组和聚合状态规则。
/// 这是任务抽屉最核心的测试集，覆盖 CLI 和 VS Code 插件两种来源。
/// </summary>
public sealed class SessionStatusStoreTests
{
    [Fact]
    public void LoadActiveSessionsSortsByPriorityAndComputesAggregateState()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("mes", CodexLightState.Red, @"F:\Mes", now.AddSeconds(-102)));
        store.Write(CreateSession("codex", CodexLightState.Yellow, @"C:\Users\28022\Desktop\codex", now.AddSeconds(-12)));
        store.Write(CreateSession("docs", CodexLightState.Green, @"C:\Users\28022\Desktop\上下文", now.AddMinutes(-1)));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Equal(3, sessions.Count);
        Assert.Equal("codex", sessions[0].SessionId);
        Assert.Equal("mes", sessions[1].SessionId);
        Assert.Equal("docs", sessions[2].SessionId);
        Assert.Equal(CodexLightState.Yellow, SessionStatusStore.GetAggregateState(sessions));
    }

    [Fact]
    public void LoadVisibleSessionsHidesOldGreenSessionsWhenProcessIsGoneAndSkipsInvalidFiles()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, _ => false);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("fresh-green", CodexLightState.Green, @"F:\Fresh", now.AddMinutes(-4)));
        store.Write(CreateSession("old-green", CodexLightState.Green, @"F:\Old", now.AddMinutes(-6)));
        Directory.CreateDirectory(paths.SessionDirectory);
        File.WriteAllText(Path.Combine(paths.SessionDirectory, "broken.json"), "{bad-json");

        var sessions = store.LoadVisibleSessions(now);

        Assert.Single(sessions);
        Assert.Equal("fresh-green", sessions[0].SessionId);
        Assert.Equal(CodexLightState.Green, SessionStatusStore.GetAggregateState(sessions));
    }

    [Fact]
    public void LoadSessionsIncludesEndedGreenSessionsWhenRequested()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, _ => false);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("fresh-green", CodexLightState.Green, @"F:\Fresh", now.AddMinutes(-4)));
        store.Write(CreateSession("old-green", CodexLightState.Green, @"F:\Old", now.AddMinutes(-45)));
        store.Write(CreateSession("stale-red", CodexLightState.Red, @"F:\Stale", now.AddMinutes(-45)));

        var visible = store.LoadVisibleSessions(now);
        var withEnded = store.LoadSessions(includeEnded: true, now);

        Assert.Single(visible);
        Assert.Equal(2, withEnded.Count);
        Assert.Contains(withEnded, session => session.SessionId == "fresh-green");
        Assert.Contains(withEnded, session => session.SessionId == "old-green");
        Assert.DoesNotContain(withEnded, session => session.SessionId == "stale-red");
    }

    [Fact]
    public void LoadVisibleSessionsHidesOldGreenSessionsEvenWhenProcessIsStillRunning()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, processId => processId == 123);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("old-green", CodexLightState.Green, @"F:\Mes", now.AddMinutes(-6), source: "cli"));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Empty(sessions);
    }

    [Fact]
    public void LoadVisibleSessionsHidesOldVsCodePluginGreenSessionsEvenWhenAppServerIsRunning()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, _ => true);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("codex-session-id", CodexLightState.Green, @"F:\Mes", now.AddMinutes(-31), source: "vscode-plugin"));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Empty(sessions);
    }

    [Fact]
    public void LoadVisibleSessionsHidesYellowAfterFiveMinutesAndRedAfterTenMinutes()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, _ => false);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("stale-yellow", CodexLightState.Yellow, @"F:\YellowOld", now.AddMinutes(-6)));
        store.Write(CreateSession("fresh-yellow", CodexLightState.Yellow, @"F:\YellowFresh", now.AddMinutes(-4)));
        store.Write(CreateSession("stale-red", CodexLightState.Red, @"F:\RedOld", now.AddMinutes(-11)));
        store.Write(CreateSession("fresh-red", CodexLightState.Red, @"F:\RedFresh", now.AddMinutes(-9)));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, session => session.SessionId == "fresh-yellow");
        Assert.Contains(sessions, session => session.SessionId == "fresh-red");
        Assert.DoesNotContain(sessions, session => session.SessionId == "stale-yellow");
        Assert.DoesNotContain(sessions, session => session.SessionId == "stale-red");
    }

    [Fact]
    public void LoadVisibleSessionsUsesCustomRetentionOptions()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, _ => false);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");
        var options = new SessionRetentionOptions
        {
            GreenRetention = TimeSpan.FromMinutes(8),
            YellowRetention = TimeSpan.FromMinutes(9),
            RedRetention = TimeSpan.FromMinutes(12),
            LiveCliWorkRetention = TimeSpan.FromHours(1),
            LiveVsCodePluginWorkRetention = TimeSpan.FromHours(1)
        };

        store.Write(CreateSession("custom-green", CodexLightState.Green, @"F:\Green", now.AddMinutes(-7)));
        store.Write(CreateSession("custom-yellow", CodexLightState.Yellow, @"F:\Yellow", now.AddMinutes(-8)));
        store.Write(CreateSession("custom-red", CodexLightState.Red, @"F:\Red", now.AddMinutes(-11)));

        var sessions = store.LoadSessions(includeEnded: false, options, now);

        Assert.Equal(3, sessions.Count);
        Assert.Contains(sessions, session => session.SessionId == "custom-green");
        Assert.Contains(sessions, session => session.SessionId == "custom-yellow");
        Assert.Contains(sessions, session => session.SessionId == "custom-red");
    }

    [Fact]
    public void LoadVisibleSessionsKeepsStaleRedAndYellowSessionsWhenProcessIsStillRunning()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, processId => processId == 123);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("long-red", CodexLightState.Red, @"F:\LongRed", now.AddMinutes(-30)));
        store.Write(CreateSession("long-yellow", CodexLightState.Yellow, @"F:\LongYellow", now.AddMinutes(-30)));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, session => session.SessionId == "long-red");
        Assert.Contains(sessions, session => session.SessionId == "long-yellow");
        Assert.Equal(CodexLightState.Yellow, SessionStatusStore.GetAggregateState(sessions));
    }

    [Fact]
    public void LoadVisibleSessionsKeepsLongRunningVsCodePluginRedSessionWhenProcessIsStillRunning()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, processId => processId == 123);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("vscode-long-red", CodexLightState.Red, @"F:\Mes", now.AddMinutes(-17), source: "vscode-plugin"));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Single(sessions);
        Assert.Equal("vscode-long-red", sessions[0].SessionId);
        Assert.Equal(CodexLightState.Red, SessionStatusStore.GetAggregateState(sessions));
    }

    [Fact]
    public void LoadVisibleSessionsHidesVeryOldVsCodePluginRedSessionEvenWhenAppServerIsRunning()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths, processId => processId == 123);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("vscode-very-old-red", CodexLightState.Red, @"F:\Mes", now.AddHours(-3), source: "vscode-plugin"));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Empty(sessions);
    }

    [Fact]
    public void WriteOverwritesSameSessionIdInsteadOfCreatingDuplicateRows()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("same-session", CodexLightState.Red, @"F:\Mes", now.AddMinutes(-1)));
        store.Write(CreateSession("same-session", CodexLightState.Green, @"F:\Mes", now));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Single(sessions);
        Assert.Equal(CodexLightState.Green, sessions[0].State);
        Assert.Single(Directory.GetFiles(paths.SessionDirectory, "*.json"));
    }

    [Fact]
    public void DeleteSessionRemovesOneSessionFile()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("keep-me", CodexLightState.Red, @"F:\Keep", now));
        store.Write(CreateSession("hide-me", CodexLightState.Yellow, @"F:\Hide", now));

        store.DeleteSession("hide-me");
        var sessions = store.LoadVisibleSessions(now);

        Assert.Single(sessions);
        Assert.Equal("keep-me", sessions[0].SessionId);
    }

    [Fact]
    public void LoadVisibleSessionsKeepsReliableSessionsWithSameWorkingDirectory()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("codex-session-a", CodexLightState.Green, @"F:\Mes", now.AddMinutes(-1)));
        store.Write(CreateSession("codex-session-b", CodexLightState.Red, @"F:\Mes", now));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, session => session.SessionId == "codex-session-a");
        Assert.Contains(sessions, session => session.SessionId == "codex-session-b");
    }

    [Fact]
    public void LoadVisibleSessionsDeduplicatesOnlyWeakPidSessionsByWorkingDirectory()
    {
        var paths = new CodexPaths(CreateTempRoot());
        var store = new SessionStatusStore(paths);
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");

        store.Write(CreateSession("pid-111-unknownstart", CodexLightState.Green, @"F:\Mes", now.AddMinutes(-1)));
        store.Write(CreateSession("pid-222-unknownstart", CodexLightState.Red, @"F:\Mes", now));

        var sessions = store.LoadVisibleSessions(now);

        Assert.Single(sessions);
        Assert.Equal("pid-222-unknownstart", sessions[0].SessionId);
    }

    [Fact]
    public void GetAggregateStateReturnsUnknownWhenNoSessionsAreVisible()
    {
        Assert.Equal(CodexLightState.Unknown, SessionStatusStore.GetAggregateState(Array.Empty<CodexSessionStatus>()));
    }

    [Fact]
    public void GetCompletionProgressTextCountsCliAndVsCodePluginSessionsTogether()
    {
        var now = DateTimeOffset.Parse("2026-06-01T15:10:00+08:00");
        var sessions = new[]
        {
            CreateSession("cli-running", CodexLightState.Red, @"F:\Mes", now, source: "cli"),
            CreateSession("cli-done", CodexLightState.Green, @"F:\Codex", now, source: "cli"),
            CreateSession("vscode-running", CodexLightState.Red, @"F:\Docs", now, source: "vscode-plugin"),
            CreateSession("vscode-done", CodexLightState.Green, @"F:\Plan", now, source: "vscode-plugin")
        };

        var text = SessionStatusStore.GetCompletionProgressText(sessions);

        Assert.Equal("2/4", text);
    }

    private static CodexSessionStatus CreateSession(
        string id,
        CodexLightState state,
        string workingDirectory,
        DateTimeOffset updatedAt,
        string source = "cli")
    {
        // 构造有代表性的 hook 快照，同时允许每个测试变更关键字段。
        return new CodexSessionStatus
        {
            SessionId = id,
            DisplayName = Path.GetFileName(workingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            WorkingDirectory = workingDirectory,
            State = state,
            Event = state switch
            {
                CodexLightState.Red => "UserPromptSubmit",
                CodexLightState.Yellow => "PermissionRequest",
                CodexLightState.Green => "Stop",
                _ => "unknown"
            },
            ProcessId = 123,
            ProcessStartTime = DateTimeOffset.Parse("2026-06-01T15:00:00+08:00"),
            UpdatedAt = updatedAt,
            Source = source
        };
    }

    private static string CreateTempRoot()
    {
        // 使用假主目录，确保会话 JSON 文件不会泄漏到真实 Codex 状态中。
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
