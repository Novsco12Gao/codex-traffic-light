using System.IO;
using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.App;

/// <summary>
/// 监听单会话 JSON 文件，并定期刷新可见性过滤。
/// </summary>
public sealed class SessionStatusDirectoryWatcher : IDisposable
{
    private readonly SessionStatusStore _store;
    private readonly Func<bool> _includeEndedSessions;
    private readonly Func<SessionRetentionOptions> _retentionOptions;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounce;
    private readonly System.Timers.Timer _refreshTimer;

    public SessionStatusDirectoryWatcher(
        CodexPaths paths,
        SessionStatusStore store,
        Func<bool>? includeEndedSessions = null,
        Func<SessionRetentionOptions>? retentionOptions = null)
    {
        _store = store;
        _includeEndedSessions = includeEndedSessions ?? (() => false);
        _retentionOptions = retentionOptions ?? (() => new SessionRetentionOptions());
        Directory.CreateDirectory(paths.SessionDirectory);

        // 会话文件会被 hook 脚本和界面创建、覆盖和删除。
        _watcher = new FileSystemWatcher(paths.SessionDirectory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnChanged;

        // 重新加载会话列表前，对快速临时文件移动做防抖。
        _debounce = new System.Timers.Timer(120) { AutoReset = false };
        _debounce.Elapsed += (_, _) => SessionsChanged?.Invoke(LoadSessions());

        // 保留窗口可能在没有文件事件时过期，因此需要偶尔轮询。
        _refreshTimer = new System.Timers.Timer(TimeSpan.FromSeconds(5)) { AutoReset = true };
        _refreshTimer.Elapsed += (_, _) => SessionsChanged?.Invoke(LoadSessions());
        _refreshTimer.Start();
    }

    /// <summary>
    /// 文件变化或刷新计时触发后，携带当前可见会话列表发出事件。
    /// </summary>
    public event Action<IReadOnlyList<CodexSessionStatus>>? SessionsChanged;

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private IReadOnlyList<CodexSessionStatus> LoadSessions()
    {
        // 加载时查询当前设置，使托盘选项立即生效。
        return _store.LoadSessions(_includeEndedSessions(), _retentionOptions());
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounce.Dispose();
        _refreshTimer.Dispose();
    }
}
