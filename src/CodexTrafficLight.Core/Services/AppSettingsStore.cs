using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 从 Codex 红绿灯设置文件读取并保存用户设置。
/// </summary>
public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create();
    private readonly CodexPaths _paths;

    public AppSettingsStore(CodexPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// 返回已保存设置；文件缺失或不可读时返回默认值。
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_paths.SettingsPath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_paths.SettingsPath), JsonOptions)
                ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// 持久化完整设置记录，替换原文件内容。
    /// </summary>
    public void Save(AppSettings settings)
    {
        _paths.EnsureCodexDirectory();
        File.WriteAllText(_paths.SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
