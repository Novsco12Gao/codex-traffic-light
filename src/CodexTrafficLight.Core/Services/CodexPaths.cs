namespace CodexTrafficLight.Core.Services;

/// <summary>
/// 解析应用在用户 Codex 主目录下使用的所有文件和目录。
/// 所有服务都通过这个类获取路径，避免各处手写路径造成不一致。
/// </summary>
public sealed class CodexPaths
{
    /// <summary>
    /// 为真实用户配置目录或测试专用主目录创建路径映射。
    /// 生产环境优先尊重 CODEX_HOME；测试传入 homeDirectory 时会强制使用隔离目录。
    /// </summary>
    public CodexPaths(string? homeDirectory = null)
    {
        HomeDirectory = string.IsNullOrWhiteSpace(homeDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : homeDirectory;

        // 仅在生产环境尊重 CODEX_HOME；测试会显式传入主目录。
        var codexHome = string.IsNullOrWhiteSpace(homeDirectory)
            ? Environment.GetEnvironmentVariable("CODEX_HOME")
            : null;

        CodexDirectory = string.IsNullOrWhiteSpace(codexHome)
            ? Path.Combine(HomeDirectory, ".codex")
            : codexHome;

        HooksPath = Path.Combine(CodexDirectory, "hooks.json");
        StatusPath = Path.Combine(CodexDirectory, "codex_traffic_light_state.json");
        SettingsPath = Path.Combine(CodexDirectory, "codex_traffic_light_settings.json");
        StatsPath = Path.Combine(CodexDirectory, "codex_traffic_light_stats.json");
        HookScriptDirectory = Path.Combine(CodexDirectory, "codex-traffic-light");
        HookScriptPath = Path.Combine(HookScriptDirectory, "codex_traffic_light_write_status.ps1");
        SessionDirectory = Path.Combine(HookScriptDirectory, "sessions");
        HookDiagnosticsDirectory = Path.Combine(HookScriptDirectory, "diagnostics");
    }

    /// <summary>
    /// 当前用户主目录。
    /// 没有传入测试目录时，该值来自系统用户配置。
    /// </summary>
    public string HomeDirectory { get; }

    /// <summary>
    /// Codex 存放 hooks、设置和红绿灯文件的根目录。
    /// 默认是 %USERPROFILE%\.codex，也可以由 CODEX_HOME 覆盖。
    /// </summary>
    public string CodexDirectory { get; }

    /// <summary>
    /// 安装器更新的 Codex hook 配置文件。
    /// 应用只替换自己拥有的 hook 项，不会清空用户其它 hook。
    /// </summary>
    public string HooksPath { get; }

    /// <summary>
    /// 主红绿灯窗口监听的聚合状态文件。
    /// 该文件保存当前兜底灯色，供没有单会话信息时使用。
    /// </summary>
    public string StatusPath { get; }

    /// <summary>
    /// 持久化应用设置文件。
    /// 例如主题、提醒模式、开机启动和会话保留时间都写入这里。
    /// </summary>
    public string SettingsPath { get; }

    /// <summary>
    /// 持久化每日统计文件。
    /// 用于统计窗口计算今日和本周的次数与时长。
    /// </summary>
    public string StatsPath { get; }

    /// <summary>
    /// 工具自有目录，包含 hook 脚本、会话状态和诊断快照。
    /// 放在单独目录下可以避免污染 Codex 根目录。
    /// </summary>
    public string HookScriptDirectory { get; }

    /// <summary>
    /// 由 Codex hook 项调用的 PowerShell hook 脚本。
    /// 安装器会自动生成并更新这个脚本。
    /// </summary>
    public string HookScriptPath { get; }

    /// <summary>
    /// 保存每个 Codex 会话 JSON 状态文件的目录。
    /// 主窗口的任务抽屉会从这里读取多会话状态。
    /// </summary>
    public string SessionDirectory { get; }

    /// <summary>
    /// 保存最近一次 hook 调用诊断快照的目录。
    /// 诊断窗口会读取这里的 latest-hook-context.json。
    /// </summary>
    public string HookDiagnosticsDirectory { get; }

    /// <summary>
    /// 写入任何自有文件前，确保 Codex 根目录存在。
    /// 目录已经存在时该调用是安全的，不会清空原有内容。
    /// </summary>
    public void EnsureCodexDirectory()
    {
        Directory.CreateDirectory(CodexDirectory);
    }
}
