using Microsoft.Win32;

namespace CodexTrafficLight.App;

/// <summary>
/// 管理当前用户的开机自动启动注册项。
/// 使用 HKCU Run 键，不需要管理员权限，也不会影响其它用户。
/// </summary>
internal static class StartupRegistrationService
{
    /// <summary>
    /// Windows 当前用户开机启动项所在的注册表路径。
    /// </summary>
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// 本应用写入 Run 键时使用的固定名称。
    /// </summary>
    private const string ValueName = "CodexTrafficLight";

    /// <summary>
    /// 判断当前用户是否已经存在本应用的开机启动项。
    /// </summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return !string.IsNullOrWhiteSpace(key?.GetValue(ValueName)?.ToString());
    }

    /// <summary>
    /// 根据用户设置写入或删除开机启动项。
    /// 启用时写入当前正在运行的程序路径；关闭时删除本应用自己的值。
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (!enabled)
        {
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            // 没有可执行文件路径时无法注册启动项，直接跳过，避免写入无效命令。
            return;
        }

        key?.SetValue(ValueName, Quote(processPath));
    }

    private static string Quote(string path)
    {
        // Run 键中的命令需要保留引号，才能正确处理安装路径里的空格。
        return "\"" + path.Trim('"') + "\"";
    }
}
