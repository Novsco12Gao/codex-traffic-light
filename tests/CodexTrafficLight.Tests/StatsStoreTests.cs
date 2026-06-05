using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

/// <summary>
/// 验证由状态转换计算出的每日和每周统计。
/// </summary>
public sealed class StatsStoreTests
{
    [Fact]
    public void RecordsRedAndGreenCounts()
    {
        var store = new StatsStore(new CodexPaths(CreateTempRoot()));
        var day = new DateOnly(2026, 6, 1);

        store.RecordStateChange(CodexLightState.Red, CodexLightState.Unknown, null, day);
        store.RecordStateChange(
            CodexLightState.Green,
            CodexLightState.Red,
            DateTimeOffset.Parse("2026-06-01T10:00:00+08:00"),
            day,
            DateTimeOffset.Parse("2026-06-01T10:05:00+08:00"));

        var stats = store.Load();

        Assert.Equal(1, stats["2026-06-01"].RedCount);
        Assert.Equal(1, stats["2026-06-01"].GreenCount);
        Assert.Equal(300000, stats["2026-06-01"].RedDurationMs);
    }

    [Fact]
    public void DoesNotRecordManualOrUnknownStates()
    {
        var store = new StatsStore(new CodexPaths(CreateTempRoot()));
        var day = new DateOnly(2026, 6, 1);

        store.RecordStateChange(CodexLightState.Yellow, CodexLightState.Unknown, null, day);
        store.RecordStateChange(CodexLightState.Unknown, CodexLightState.Yellow, null, day);

        var stats = store.Load();

        Assert.Equal(0, stats["2026-06-01"].RedCount);
        Assert.Equal(0, stats["2026-06-01"].GreenCount);
        Assert.Equal(0, stats["2026-06-01"].RedDurationMs);
    }

    [Fact]
    public void RecordsAggregateSessionStateChangesForWeeklyReport()
    {
        var store = new StatsStore(new CodexPaths(CreateTempRoot()));
        var day = new DateOnly(2026, 6, 1);
        var redStartedAt = DateTimeOffset.Parse("2026-06-01T10:00:00+08:00");

        store.RecordStateChange(CodexLightState.Red, CodexLightState.Unknown, null, day, redStartedAt);
        store.RecordStateChange(
            CodexLightState.Green,
            CodexLightState.Red,
            redStartedAt,
            day,
            DateTimeOffset.Parse("2026-06-01T10:03:00+08:00"));

        var stats = store.Load();

        Assert.Equal(1, stats["2026-06-01"].RedCount);
        Assert.Equal(1, stats["2026-06-01"].GreenCount);
        Assert.Equal(180000, stats["2026-06-01"].RedDurationMs);
    }

    [Fact]
    public void GetsTodayAndCurrentWeekSummaries()
    {
        var store = new StatsStore(new CodexPaths(CreateTempRoot()));
        var monday = new DateOnly(2026, 6, 1);
        var today = new DateOnly(2026, 6, 3);

        store.RecordStateChange(CodexLightState.Red, CodexLightState.Unknown, null, monday);
        store.RecordStateChange(CodexLightState.Green, CodexLightState.Red, DateTimeOffset.Parse("2026-06-01T10:00:00+08:00"), monday, DateTimeOffset.Parse("2026-06-01T10:02:00+08:00"));
        store.RecordStateChange(CodexLightState.Red, CodexLightState.Unknown, null, today);
        store.RecordStateChange(CodexLightState.Green, CodexLightState.Red, DateTimeOffset.Parse("2026-06-03T10:00:00+08:00"), today, DateTimeOffset.Parse("2026-06-03T10:04:00+08:00"));

        var todaySummary = store.GetTodaySummary(today);
        var weekSummary = store.GetCurrentWeekSummary(today);

        Assert.Equal(1, todaySummary.RedCount);
        Assert.Equal(1, todaySummary.GreenCount);
        Assert.Equal(240000, todaySummary.RedDurationMs);
        Assert.Equal(240000, todaySummary.AverageRedDurationMs);
        Assert.Equal(2, weekSummary.RedCount);
        Assert.Equal(2, weekSummary.GreenCount);
        Assert.Equal(360000, weekSummary.RedDurationMs);
        Assert.Equal(180000, weekSummary.AverageRedDurationMs);
    }

    private static string CreateTempRoot()
    {
        // 统计是文件支撑的，因此每个测试都写入隔离临时根目录。
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
