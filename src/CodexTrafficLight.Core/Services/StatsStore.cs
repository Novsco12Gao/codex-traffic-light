using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 维护 Codex 活动的每日次数和工作总时长。
/// </summary>
public sealed class StatsStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create();
    private readonly CodexPaths _paths;

    public StatsStore(CodexPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// 加载全部每日统计；没有可用文件时返回空映射。
    /// </summary>
    public Dictionary<string, DailyStats> Load()
    {
        try
        {
            if (!File.Exists(_paths.StatsPath))
            {
                return new Dictionary<string, DailyStats>();
            }

            return JsonSerializer.Deserialize<Dictionary<string, DailyStats>>(File.ReadAllText(_paths.StatsPath), JsonOptions)
                ?? new Dictionary<string, DailyStats>();
        }
        catch
        {
            return new Dictionary<string, DailyStats>();
        }
    }

    /// <summary>
    /// 汇总本地当天或测试注入的日期。
    /// </summary>
    public StatsSummary GetTodaySummary(DateOnly? today = null)
    {
        var day = today ?? DateOnly.FromDateTime(DateTime.Now);
        return SumRange(day, day);
    }

    /// <summary>
    /// 汇总包含当前或注入日期的周一到周日。
    /// </summary>
    public StatsSummary GetCurrentWeekSummary(DateOnly? today = null)
    {
        var end = today ?? DateOnly.FromDateTime(DateTime.Now);
        var diff = ((int)end.DayOfWeek + 6) % 7;
        var start = end.AddDays(-diff);
        return SumRange(start, start.AddDays(6));
    }

    /// <summary>
    /// 聚合状态变化时记录计数和红灯持续时长。
    /// </summary>
    public void RecordStateChange(
        CodexLightState newState,
        CodexLightState previousState,
        DateTimeOffset? redStartedAt,
        DateOnly? localDay = null,
        DateTimeOffset? now = null)
    {
        var currentTime = now ?? DateTimeOffset.Now;
        var key = (localDay ?? DateOnly.FromDateTime(currentTime.LocalDateTime)).ToString("yyyy-MM-dd");
        var all = Load();
        all.TryGetValue(key, out var current);
        current ??= new DailyStats();

        // 开始工作时增加红灯次数；变为绿灯时同时结算已测量的红灯区间。
        var next = newState switch
        {
            CodexLightState.Red => current with { RedCount = current.RedCount + 1 },
            CodexLightState.Green => current with
            {
                GreenCount = current.GreenCount + 1,
                RedDurationMs = current.RedDurationMs + CalculateRedDuration(previousState, redStartedAt, currentTime)
            },
            _ => current
        };

        all[key] = next;
        Save(all);
    }

    private static long CalculateRedDuration(CodexLightState previousState, DateTimeOffset? redStartedAt, DateTimeOffset now)
    {
        if (previousState != CodexLightState.Red || redStartedAt is null)
        {
            return 0;
        }

        var duration = now - redStartedAt.Value;
        return duration < TimeSpan.Zero ? 0 : (long)duration.TotalMilliseconds;
    }

    private void Save(Dictionary<string, DailyStats> stats)
    {
        _paths.EnsureCodexDirectory();
        File.WriteAllText(_paths.StatsPath, JsonSerializer.Serialize(stats, JsonOptions));
    }

    private StatsSummary SumRange(DateOnly start, DateOnly end)
    {
        var all = Load();
        var redCount = 0;
        var greenCount = 0;
        long redDuration = 0;

        // 按日历日期遍历，而不是假设持久化映射中的键连续。
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (!all.TryGetValue(day.ToString("yyyy-MM-dd"), out var stats))
            {
                continue;
            }

            redCount += stats.RedCount;
            greenCount += stats.GreenCount;
            redDuration += stats.RedDurationMs;
        }

        return new StatsSummary
        {
            RedCount = redCount,
            GreenCount = greenCount,
            RedDurationMs = redDuration
        };
    }
}
