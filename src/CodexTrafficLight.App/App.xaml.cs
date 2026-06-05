using System.Threading;
using System.Windows;

namespace CodexTrafficLight.App;

/// <summary>
/// WPF 应用入口，负责保证只运行一个实例。
/// </summary>
public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 命名本地互斥体用于防止重复托盘图标和多个文件监听器竞争。
        _singleInstanceMutex = new Mutex(true, "Local\\CodexTrafficLight.SingleInstance", out _ownsMutex);
        if (!_ownsMutex)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 仅在当前进程启动时取得所有权后才释放互斥体。
        if (_ownsMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
