using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 读取并写入聚合红绿灯状态文件。
/// </summary>
public sealed class StatusFileStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create(includeEnumConverter: true);
    private readonly CodexPaths _paths;

    public StatusFileStore(CodexPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// 读取当前状态；文件缺失或无效时返回安全占位状态。
    /// </summary>
    public CodexStatus Read()
    {
        try
        {
            if (!File.Exists(_paths.StatusPath))
            {
                return new CodexStatus(CodexLightState.Unknown, "missing", DateTimeOffset.Now);
            }

            var json = File.ReadAllText(_paths.StatusPath);
            return JsonSerializer.Deserialize<CodexStatus>(json, JsonOptions)
                ?? new CodexStatus(CodexLightState.Unknown, "invalid", DateTimeOffset.Now);
        }
        catch
        {
            return new CodexStatus(CodexLightState.Unknown, "error", DateTimeOffset.Now);
        }
    }

    /// <summary>
    /// 以原子方式写入状态，避免文件监听器看到半写入 JSON。
    /// </summary>
    public void Write(CodexStatus status)
    {
        _paths.EnsureCodexDirectory();
        var tempPath = _paths.StatusPath + ".tmp";
        var json = JsonSerializer.Serialize(status, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _paths.StatusPath, overwrite: true);
    }

    /// <summary>
    /// 供只知道新状态和事件名的调用方使用的便捷重载。
    /// </summary>
    public void Write(CodexLightState state, string eventName)
    {
        Write(new CodexStatus(state, eventName, DateTimeOffset.Now));
    }
}
