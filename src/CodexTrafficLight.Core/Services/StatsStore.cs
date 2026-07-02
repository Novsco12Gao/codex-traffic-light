using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 维护 Codex 活动的每日次数和工作总时长。
/// 它只记录状态变化后的统计结果，不保存完整会话明细。
/// </summary>
public sealed class StatsStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create();
    private readonly CodexPaths _paths;

    /// <summary>
    /// 使用统一路径对象定位统计文件，方便测试用临时目录隔离真实数据。
    /// </summary>
    public StatsStore(CodexPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// 加载全部每日统计。
    /// 没有可用文件、文件损坏或反序列化失败时返回空映射，避免统计窗口影响主程序启动。
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
    /// 返回值用于统计窗口中的“今天”区域。
    /// </summary>
    public StatsSummary GetTodaySummary(DateOnly? today = null)
    {
        var day = today ?? DateOnly.FromDateTime(DateTime.Now);
        return SumRange(day, day);
    }

    /// <summary>
    /// 汇总包含当前或注入日期的周一到周日。
    /// 周起点固定为周一，便于国内用户按自然周理解。
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
    /// 只有状态真正变化时调用，重复的同色刷新不会在这里重复累加。
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
        // 只有从红灯离开时才结算持续时间；其它状态变化没有红灯时长。
        if (previousState != CodexLightState.Red || redStartedAt is null)
        {
            return 0;
        }

        var duration = now - redStartedAt.Value;
        return duration < TimeSpan.Zero ? 0 : (long)duration.TotalMilliseconds;
    }

    private void Save(Dictionary<string, DailyStats> stats)
    {
        // 统计文件体积很小，直接整体覆盖比增量写入更简单可靠。
        _paths.EnsureCodexDirectory();
        File.WriteAllText(_paths.StatsPath, JsonSerializer.Serialize(stats, JsonOptions));
    }

    private StatsSummary SumRange(DateOnly start, DateOnly end)
    {
        // 每次汇总都从持久化文件读取，保证统计窗口反映最新落盘数据。
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
