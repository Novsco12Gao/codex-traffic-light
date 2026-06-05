using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 保存并过滤 Codex hooks 写入的各会话红绿灯快照。
/// </summary>
public sealed class SessionStatusStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create(includeEnumConverter: true);

    // 已完成或等待中的会话会较快隐藏，仍在工作的活动会话
    // 在所属进程仍存在时可以保持更长时间可见。
    private static readonly TimeSpan GreenRetention = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan YellowRetention = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RedRetention = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LiveCliWorkRetention = TimeSpan.FromHours(6);
    private static readonly TimeSpan LiveVsCodePluginWorkRetention = TimeSpan.FromHours(2);
    private readonly CodexPaths _paths;
    private readonly Func<int, bool> _isProcessRunning;

    public SessionStatusStore(CodexPaths paths, Func<int, bool>? isProcessRunning = null)
    {
        _paths = paths;
        _isProcessRunning = isProcessRunning ?? IsProcessRunning;
    }

    /// <summary>
    /// 通过临时文件移动写入单个会话快照，避免读到半写入内容。
    /// </summary>
    public void Write(CodexSessionStatus status)
    {
        Directory.CreateDirectory(_paths.SessionDirectory);
        var path = GetSessionPath(status.SessionId);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(status, JsonOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// 为主抽屉加载活动或最近完成的会话。
    /// </summary>
    public IReadOnlyList<CodexSessionStatus> LoadVisibleSessions(DateTimeOffset? now = null)
    {
        return LoadSessions(includeEnded: false, now);
    }

    public IReadOnlyList<CodexSessionStatus> LoadVisibleSessions(SessionRetentionOptions options, DateTimeOffset? now = null)
    {
        return LoadSessions(includeEnded: false, options, now);
    }

    /// <summary>
    /// 应用可见性、去重和显示排序规则后加载会话。
    /// </summary>
    public IReadOnlyList<CodexSessionStatus> LoadSessions(bool includeEnded, DateTimeOffset? now = null)
    {
        return LoadSessions(includeEnded, new SessionRetentionOptions(), now);
    }

    public IReadOnlyList<CodexSessionStatus> LoadSessions(bool includeEnded, SessionRetentionOptions options, DateTimeOffset? now = null)
    {
        return LoadAllSessions()
            .Where(session => IsVisible(session, now ?? DateTimeOffset.Now, includeEnded, options))
            // 由 PID 派生的弱身份可能在多次 hook 间变化，因此按工作目录分组。
            .GroupBy(GetSessionGroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(session => session.UpdatedAt).First())
            .OrderBy(GetPriority)
            .ThenByDescending(session => session.UpdatedAt)
            .ToList();
    }

    /// <summary>
    /// 读取所有格式正确的会话文件，不应用可见性过滤。
    /// </summary>
    public IReadOnlyList<CodexSessionStatus> LoadAllSessions()
    {
        if (!Directory.Exists(_paths.SessionDirectory))
        {
            return Array.Empty<CodexSessionStatus>();
        }

        var sessions = new List<CodexSessionStatus>();
        foreach (var path in Directory.EnumerateFiles(_paths.SessionDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(path);
                var session = JsonSerializer.Deserialize<CodexSessionStatus>(json, JsonOptions);
                if (session is not null && !string.IsNullOrWhiteSpace(session.SessionId))
                {
                    sessions.Add(session);
                }
            }
            catch
            {
                // 忽略单个损坏的会话文件，其他 CLI 窗口仍应正常渲染。
            }
        }

        return sessions;
    }

    /// <summary>
    /// 超过正常绿灯保留窗口后删除已完成会话文件。
    /// </summary>
    public void ClearEndedSessions(DateTimeOffset? now = null)
    {
        ClearEndedSessions(new SessionRetentionOptions(), now);
    }

    public void ClearEndedSessions(SessionRetentionOptions options, DateTimeOffset? now = null)
    {
        if (!Directory.Exists(_paths.SessionDirectory))
        {
            return;
        }

        var cutoff = (now ?? DateTimeOffset.Now) - options.GreenRetention;
        foreach (var session in LoadAllSessions().Where(session => session.State == CodexLightState.Green && session.UpdatedAt < cutoff))
        {
            var path = GetSessionPath(session.SessionId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// 按外部会话标识删除一个会话文件。
    /// </summary>
    public void DeleteSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var path = GetSessionPath(sessionId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 将多个会话颜色折叠为主灯显示的单一颜色。
    /// </summary>
    public static CodexLightState GetAggregateState(IEnumerable<CodexSessionStatus> sessions)
    {
        var list = sessions.ToList();
        if (list.Count == 0)
        {
            return CodexLightState.Unknown;
        }

        if (list.Any(session => session.State == CodexLightState.Yellow))
        {
            return CodexLightState.Yellow;
        }

        if (list.Any(session => session.State == CodexLightState.Red))
        {
            return CodexLightState.Red;
        }

        return list.All(session => session.State == CodexLightState.Green)
            ? CodexLightState.Green
            : CodexLightState.Unknown;
    }

    /// <summary>
    /// 格式化抽屉摘要中使用的已完成会话数量。
    /// </summary>
    public static string GetCompletionProgressText(IEnumerable<CodexSessionStatus> sessions)
    {
        var list = sessions.ToList();
        var completed = list.Count(session => session.State == CodexLightState.Green);
        return $"{completed}/{list.Count}";
    }

    private string GetSessionPath(string sessionId)
    {
        var fileName = SanitizeSessionId(sessionId) + ".json";
        return Path.Combine(_paths.SessionDirectory, fileName);
    }

    private bool IsVisible(CodexSessionStatus session, DateTimeOffset now, bool includeEnded, SessionRetentionOptions options)
    {
        var age = now - session.UpdatedAt;
        if (session.State == CodexLightState.Green)
        {
            if (includeEnded)
            {
                return true;
            }

            return age <= options.GreenRetention;
        }

        // 过期的红灯/黄灯状态仅在来源进程仍存活时保留。
        return session.State switch
        {
            CodexLightState.Yellow => age <= options.YellowRetention || IsLiveWork(session, age, options),
            CodexLightState.Red => age <= options.RedRetention || IsLiveWork(session, age, options),
            _ => age <= options.RedRetention
        };
    }

    private bool IsLiveWork(CodexSessionStatus session, TimeSpan age, SessionRetentionOptions options)
    {
        if (session.ProcessId <= 0 || !_isProcessRunning(session.ProcessId))
        {
            return false;
        }

        // CLI 运行可能持续很久；VS Code 插件任务预期会更快刷新。
        if (session.Source.Equals("cli", StringComparison.OrdinalIgnoreCase))
        {
            return age <= options.LiveCliWorkRetention;
        }

        if (session.Source.Equals("vscode-plugin", StringComparison.OrdinalIgnoreCase))
        {
            return age <= options.LiveVsCodePluginWorkRetention;
        }

        return false;
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static int GetPriority(CodexSessionStatus session)
    {
        // 等待用户处理的会话最需要优先显示，其次是正在工作的会话。
        return session.State switch
        {
            CodexLightState.Yellow => 0,
            CodexLightState.Red => 1,
            CodexLightState.Green => 2,
            _ => 3
        };
    }

    private static string GetSessionGroupKey(CodexSessionStatus session)
    {
        // 旧 hook 载荷可能只能识别启动时间未知的进程。
        if (IsWeakSessionIdentity(session.SessionId) && !string.IsNullOrWhiteSpace(session.WorkingDirectory))
        {
            return "weak:" + session.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return "session:" + session.SessionId;
    }

    private static bool IsWeakSessionIdentity(string sessionId)
    {
        return sessionId.StartsWith("pid-", StringComparison.OrdinalIgnoreCase) &&
               sessionId.Contains("unknownstart", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeSessionId(string sessionId)
    {
        // 保持文件名可移植，并防止 hook 提供的 ID 造成路径穿越。
        var chars = sessionId
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray();
        return chars.Length == 0 ? "unknown" : new string(chars);
    }
}
