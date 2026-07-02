using System.IO;
using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.App;

/// <summary>
/// 监听聚合状态 JSON 文件，并发出防抖后的状态更新。
/// 主窗口通过它接收 hook 或手动切灯写入的兜底状态变化。
/// </summary>
public sealed class StatusFileWatcher : IDisposable
{
    private readonly StatusFileStore _store;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounce;

    /// <summary>
    /// 创建状态文件监听器，并确保 Codex 配置目录存在。
    /// </summary>
    public StatusFileWatcher(CodexPaths paths, StatusFileStore store)
    {
        _store = store;
        Directory.CreateDirectory(paths.CodexDirectory);

        // 只监听状态文件名，避免无关 Codex 写入唤醒界面。
        _watcher = new FileSystemWatcher(paths.CodexDirectory, Path.GetFileName(paths.StatusPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnChanged;

        // 一次原子替换可能让 FileSystemWatcher 发出多个事件。
        _debounce = new System.Timers.Timer(120) { AutoReset = false };
        _debounce.Elapsed += (_, _) => StatusChanged?.Invoke(_store.Read());
    }

    /// <summary>
    /// 稳定的状态文件变化被读取后触发。
    /// 事件回调来自计时器线程，使用方需要按自己的 UI 线程规则切换。
    /// </summary>
    public event Action<CodexStatus>? StatusChanged;

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        // 每次文件事件都重置防抖计时，等写入稳定后再读取文件。
        _debounce.Stop();
        _debounce.Start();
    }

    public void Dispose()
    {
        // 同时释放非托管监听器句柄和防抖计时器。
        _watcher.Dispose();
        _debounce.Dispose();
    }
}
