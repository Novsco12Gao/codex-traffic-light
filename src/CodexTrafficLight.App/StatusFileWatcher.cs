using System.IO;
using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;

namespace CodexTrafficLight.App;

/// <summary>
/// 监听聚合状态 JSON 文件，并发出防抖后的状态更新。
/// </summary>
public sealed class StatusFileWatcher : IDisposable
{
    private readonly StatusFileStore _store;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounce;

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
    /// </summary>
    public event Action<CodexStatus>? StatusChanged;

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
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
