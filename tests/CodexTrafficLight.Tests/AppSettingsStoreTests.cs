using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.Tests;

/// <summary>
/// 验证设置持久化，并避免触碰真实用户配置目录。
/// 默认值测试保护首次启动体验，往返测试保护新增设置字段不会丢失。
/// </summary>
public sealed class AppSettingsStoreTests
{
    [Fact]
    public void LoadReturnsDefaultsWhenMissing()
    {
        var store = new AppSettingsStore(new CodexPaths(CreateTempRoot()));

        var settings = store.Load();

        Assert.Equal("dark", settings.Theme);
        Assert.Equal("triple", settings.Style);
        Assert.False(settings.Muted);
        Assert.True(settings.AutoOpenDrawerOnYellow);
        Assert.True(settings.AutoLaunchOnCodexActivity);
        Assert.False(settings.ShowEndedSessions);
        Assert.False(settings.StartWithWindows);
        Assert.Equal("balloon", settings.ReminderMode);
        Assert.Equal(5, settings.GreenRetentionMinutes);
        Assert.Equal(5, settings.YellowRetentionMinutes);
        Assert.Equal(10, settings.RedRetentionMinutes);
        Assert.Equal(6, settings.LiveCliWorkRetentionHours);
        Assert.Equal(2, settings.LiveVsCodePluginWorkRetentionHours);
        Assert.True(settings.Topmost);
        Assert.Equal("breath", settings.LampEffect);
        Assert.Equal("standard", settings.LampSpeed);
        Assert.Null(settings.WindowLeft);
        Assert.Null(settings.WindowTop);
    }

    [Fact]
    public void SaveAndLoadRoundTrips()
    {
        var store = new AppSettingsStore(new CodexPaths(CreateTempRoot()));
        var expected = new AppSettings
        {
            WindowLeft = 100,
            WindowTop = 80,
            Theme = "light",
            Style = "single",
            Muted = true,
            AutoOpenDrawerOnYellow = false,
            AutoLaunchOnCodexActivity = false,
            ShowEndedSessions = true,
            StartWithWindows = true,
            ReminderMode = "balloon-sound",
            GreenRetentionMinutes = 8,
            YellowRetentionMinutes = 9,
            RedRetentionMinutes = 12,
            LiveCliWorkRetentionHours = 7,
            LiveVsCodePluginWorkRetentionHours = 3,
            Topmost = false,
            LampEffect = "steady",
            LampSpeed = "slow"
        };

        store.Save(expected);
        var actual = store.Load();

        Assert.Equal(expected.WindowLeft, actual.WindowLeft);
        Assert.Equal(expected.WindowTop, actual.WindowTop);
        Assert.Equal("light", actual.Theme);
        Assert.Equal("single", actual.Style);
        Assert.True(actual.Muted);
        Assert.False(actual.AutoOpenDrawerOnYellow);
        Assert.False(actual.AutoLaunchOnCodexActivity);
        Assert.True(actual.ShowEndedSessions);
        Assert.True(actual.StartWithWindows);
        Assert.Equal("balloon-sound", actual.ReminderMode);
        Assert.Equal(8, actual.GreenRetentionMinutes);
        Assert.Equal(9, actual.YellowRetentionMinutes);
        Assert.Equal(12, actual.RedRetentionMinutes);
        Assert.Equal(7, actual.LiveCliWorkRetentionHours);
        Assert.Equal(3, actual.LiveVsCodePluginWorkRetentionHours);
        Assert.False(actual.Topmost);
        Assert.Equal("steady", actual.LampEffect);
        Assert.Equal("slow", actual.LampSpeed);
    }

    private static string CreateTempRoot()
    {
        // 每个测试都在系统临时目录下获得隔离的假主目录。
        var path = Path.Combine(Path.GetTempPath(), "CodexTrafficLightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
