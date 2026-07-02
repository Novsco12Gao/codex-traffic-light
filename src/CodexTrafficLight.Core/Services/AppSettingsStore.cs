using System.Text.Json;
using CodexTrafficLight.Core.Models;

namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 从 Codex 红绿灯设置文件读取并保存用户设置。
/// 这个类只负责本地设置的持久化，不参与界面状态计算。
/// </summary>
public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = JsonOptionsFactory.Create();
    private readonly CodexPaths _paths;

    /// <summary>
    /// 使用统一路径对象定位设置文件，方便正式环境和测试环境共用同一套读写逻辑。
    /// </summary>
    public AppSettingsStore(CodexPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// 返回已保存设置。
    /// 文件缺失、内容损坏或字段不完整时，都会返回带默认值的新设置对象，避免启动失败。
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
    /// 保存前会确保 Codex 配置目录存在，首次运行时也能直接写入。
    /// </summary>
    public void Save(AppSettings settings)
    {
        _paths.EnsureCodexDirectory();
        File.WriteAllText(_paths.SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
