using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 读取并写入聚合红绿灯状态文件。
/// 这个文件是单会话状态不可用时的兜底来源，也是手动切灯时写入的位置。
/// </summary>
public sealed class StatusFileStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create(includeEnumConverter: true);
    private readonly CodexPaths _paths;

    /// <summary>
    /// 使用统一路径对象定位状态文件，避免各处重复拼接 Codex 目录。
    /// </summary>
    public StatusFileStore(CodexPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// 读取当前状态。
    /// 文件缺失、内容无效或读取失败时返回未知状态，让界面保持可启动。
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
    /// 以原子方式写入状态。
    /// 先写临时文件再移动覆盖，避免文件监听器读到半写入的 JSON。
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
    /// 写入时间会在这里统一取当前本地时间。
    /// </summary>
    public void Write(CodexLightState state, string eventName)
    {
        Write(new CodexStatus(state, eventName, DateTimeOffset.Now));
    }
}
