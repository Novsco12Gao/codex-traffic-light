using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Net.Http;
using System.Media;
using System.Text;
using CodexTrafficLight.Core.Models;
using CodexTrafficLight.Core.Services;
using Forms = System.Windows.Forms;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;
using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfMessageBox = System.Windows.MessageBox;

namespace CodexTrafficLight.App;

/// <summary>
/// 主悬浮红绿灯窗口，同时负责托盘菜单和界面协调。
/// </summary>
public partial class MainWindow : Window
{
    private readonly CodexPaths _paths = new();
    private readonly StatusFileStore _statusStore;
    private readonly StatusFileWatcher _watcher;
    private readonly StatsStore _statsStore;
    private readonly SessionStatusStore _sessionStatusStore;
    private readonly SessionStatusDirectoryWatcher _sessionWatcher;
    private readonly Dictionary<CodexLightState, Ellipse> _lamps;
    private readonly Dictionary<CodexLightState, Ellipse> _wells;
    private readonly Dictionary<CodexLightState, Ellipse> _rings;
    private readonly Dictionary<CodexLightState, FrameworkElement> _lampHosts;
    private static readonly Duration LampTransitionDuration = new(TimeSpan.FromMilliseconds(200));
    private const string UpdateManifestUrl = "https://raw.githubusercontent.com/Novsco12Gao/codex-traffic-light/main/version.json";

    private AppSettingsStore? _settingsStore;
    private AppSettings _settings = new();
    private Forms.NotifyIcon? _notifyIcon;
    private CodexLightState _currentState = CodexLightState.Unknown;
    private CodexLightState _lastStatsState = CodexLightState.Unknown;
    private CodexLightState _lastReminderState = CodexLightState.Unknown;
    private DateTimeOffset? _redStartedAt;
    private readonly bool _shouldShowTrustReminder;
    private IReadOnlyList<CodexSessionStatus> _visibleSessions = Array.Empty<CodexSessionStatus>();
    private CodexLightState _lastAggregateState = CodexLightState.Unknown;
    private DateTimeOffset _drawerSuppressedUntil = DateTimeOffset.MinValue;
    private readonly DispatcherTimer _drawerAutoCloseTimer;
    private bool _statsInitialized;
    private static string AboutText => $"""
Codex 红绿灯

状态说明：
红灯：Codex 正在处理当前请求。
黄灯：等待权限确认，需要你操作。
绿灯：当前轮次已结束或空闲。
灰灯：暂无可用状态。

多任务时：
主灯按 黄 > 红 > 绿 的优先级显示；
右侧任务面板显示每个 Codex 会话的状态。

技术栈：
.NET 8 + WPF
Windows Forms 托盘菜单
Codex Hooks + PowerShell
本地 JSON 状态文件
FileSystemWatcher 实时监听
xUnit 自动化测试

纯本地运行，不上传数据。

版本：{GetCurrentVersion()}

愿每一次红灯，都是通往绿灯的一步。

作者：Gyk
""";

    public MainWindow()
    {
        InitializeComponent();

        // 核心存储会被文件监听器、手动托盘操作和启动初始化共同使用。
        _statusStore = new StatusFileStore(_paths);
        _watcher = new StatusFileWatcher(_paths, _statusStore);
        _statsStore = new StatsStore(_paths);
        _sessionStatusStore = new SessionStatusStore(_paths);
        _sessionWatcher = new SessionStatusDirectoryWatcher(
            _paths,
            _sessionStatusStore,
            () => _settings.ShowEndedSessions,
            () => _settings.ToSessionRetentionOptions());
        _settingsStore = new AppSettingsStore(_paths);
        _shouldShowTrustReminder = !File.Exists(_paths.SettingsPath);
        _settings = _settingsStore.Load();
        _drawerAutoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _drawerAutoCloseTimer.Tick += (_, _) =>
        {
            _drawerAutoCloseTimer.Stop();
            SetDrawerOpen(false, suppressAutoOpen: false);
        };

        // 查找表让状态到视觉元素的映射保持紧凑且一致。
        _lamps = new Dictionary<CodexLightState, Ellipse>
        {
            [CodexLightState.Red] = RedLamp,
            [CodexLightState.Yellow] = YellowLamp,
            [CodexLightState.Green] = GreenLamp
        };
        _wells = new Dictionary<CodexLightState, Ellipse>
        {
            [CodexLightState.Red] = RedWell,
            [CodexLightState.Yellow] = YellowWell,
            [CodexLightState.Green] = GreenWell
        };
        _rings = new Dictionary<CodexLightState, Ellipse>
        {
            [CodexLightState.Red] = RedRing,
            [CodexLightState.Yellow] = YellowRing,
            [CodexLightState.Green] = GreenRing
        };
        _lampHosts = new Dictionary<CodexLightState, FrameworkElement>
        {
            [CodexLightState.Red] = RedLampHost,
            [CodexLightState.Yellow] = YellowLampHost,
            [CodexLightState.Green] = GreenLampHost
        };

        ApplySavedWindowPosition();
        Topmost = _settings.Topmost;
        MuteCheckBox.IsChecked = _settings.Muted;
        ThemeCheckBox.IsChecked = _settings.Theme == "dark";
        ApplyTheme(_settings.Theme);
        ApplyStatus(_statusStore.Read());

        // 监听器回调来自后台线程，必须切回 WPF UI 线程。
        _watcher.StatusChanged += status =>
        {
            Dispatcher.Invoke(() => ApplyStatus(status));
        };
        _sessionWatcher.SessionsChanged += sessions =>
        {
            Dispatcher.Invoke(() => ApplySessions(sessions));
        };

        CreateTrayMenu();
        InstallHooksAtStartup();
        ApplySessions(LoadCurrentSessions());
        _statsInitialized = true;
    }

    public void ApplyStatus(CodexStatus status)
    {
        // 单会话文件比聚合兜底状态文件更具体。
        if (_visibleSessions.Count > 0)
        {
            return;
        }

        ApplyState(status.State);

        if (status.Event is "manual" or "app-start")
        {
            return;
        }

        RecordStatsState(status.State);
    }

    private void RecordStatsState(CodexLightState state)
    {
        // 忽略未知状态和重复状态，使统计只记录状态转换。
        if (state == CodexLightState.Unknown || state == _lastStatsState)
        {
            return;
        }

        if (!_statsInitialized)
        {
            // 初始化基线时不记录启动时加载到的旧状态。
            _redStartedAt = state == CodexLightState.Red ? DateTimeOffset.Now : null;
            _lastStatsState = state;
            return;
        }

        _statsStore.RecordStateChange(state, _lastStatsState, _redStartedAt);
        _redStartedAt = state == CodexLightState.Red ? DateTimeOffset.Now : null;
        _lastStatsState = state;
    }

    public void ApplySessions(IReadOnlyList<CodexSessionStatus> sessions)
    {
        // 抽屉展示单个会话，主灯展示这些会话的聚合状态。
        _visibleSessions = sessions;
        var aggregateState = SessionStatusStore.GetAggregateState(sessions);
        var hasMultipleSessions = sessions.Count > 1;

        SessionCountBadge.Visibility = hasMultipleSessions ? Visibility.Visible : Visibility.Collapsed;
        SessionCountText.Text = SessionStatusStore.GetCompletionProgressText(sessions);
        RenderSessionRows(sessions);

        if (sessions.Count > 0)
        {
            ApplyState(aggregateState);
            RecordStatsState(aggregateState);
        }

        if (!hasMultipleSessions)
        {
            SetDrawerOpen(false, suppressAutoOpen: false);
        }
        else if (_settings.AutoOpenDrawerOnYellow &&
                 aggregateState == CodexLightState.Yellow &&
                 _lastAggregateState != CodexLightState.Yellow &&
                 DateTimeOffset.Now >= _drawerSuppressedUntil)
        {
            SetDrawerOpen(true, suppressAutoOpen: false);
            _drawerAutoCloseTimer.Stop();
            _drawerAutoCloseTimer.Start();
        }

        _lastAggregateState = aggregateState;
    }

    public void ApplyState(CodexLightState state)
    {
        // 从头重建视觉状态，避免 WPF 动画互相叠加。
        _currentState = state;
        StopAnimations();
        ShowYellowReminderIfNeeded(state);

        foreach (var (lampState, lamp) in _lamps)
        {
            ApplyLampVisual(lampState, lamp, _wells[lampState], _rings[lampState], lampState == state);
        }

        ApplyStyle(_settings.Style);
    }

    private void ApplyLampVisual(CodexLightState state, Ellipse lamp, Ellipse well, Ellipse ring, bool active)
    {
        // 每个颜色位置的灯、灯槽、发光和光环会一起更新。
        var config = GetLampConfig(state);
        var lampBrush = CreateLampBrush(config, active);
        lamp.Fill = lampBrush;
        lamp.BeginAnimation(OpacityProperty, CreateDoubleAnimation(1));

        var wellBrush = CreateWellBrush(config, active);
        well.Fill = wellBrush;
        well.BeginAnimation(OpacityProperty, CreateDoubleAnimation(1));

        var lampGlow = CreateLampGlow(config.GlowColor, active ? 12 : 0, active ? 0.9 : 0);
        lamp.Effect = lampGlow;

        var wellGlow = CreateLampGlow(config.GlowColor, active ? 40 : 0, active ? 0.82 : 0);
        well.Effect = wellGlow;

        ring.Fill = new SolidColorBrush(config.ActiveColor);
        ring.Effect = CreateLampGlow(config.GlowColor, 28, 0.7);
        ring.Opacity = 0;
        ResetRingScale(ring);

        if (!active || _settings.LampEffect.Equals("steady", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // 红灯用呼吸效果表示正在工作；黄灯和绿灯用脉冲表示参考状态。
        if (state == CodexLightState.Red)
        {
            StartBreath(lamp, lampGlow, wellGlow, GetLampEffectDuration());
            return;
        }

        StartReferencePulse(lamp, ring, GetLampEffectDuration());
    }

    private static LampVisualConfig GetLampConfig(CodexLightState state)
    {
        // 将颜色常量集中在一处，保证所有灯元素使用同一套配色。
        return state switch
        {
            CodexLightState.Red => new LampVisualConfig(
                MediaColor.FromRgb(255, 59, 48),
                MediaColor.FromRgb(75, 20, 20),
                MediaColor.FromArgb(140, 255, 59, 48),
                MediaColor.FromArgb(76, 255, 59, 48)),
            CodexLightState.Yellow => new LampVisualConfig(
                MediaColor.FromRgb(255, 159, 10),
                MediaColor.FromRgb(75, 55, 15),
                MediaColor.FromArgb(140, 255, 159, 10),
                MediaColor.FromArgb(76, 255, 159, 10)),
            CodexLightState.Green => new LampVisualConfig(
                MediaColor.FromRgb(48, 209, 88),
                MediaColor.FromRgb(20, 75, 35),
                MediaColor.FromArgb(140, 48, 209, 88),
                MediaColor.FromArgb(76, 48, 209, 88)),
            _ => new LampVisualConfig(
                MediaColor.FromRgb(96, 96, 96),
                MediaColor.FromRgb(36, 36, 36),
                MediaColor.FromArgb(0, 96, 96, 96),
                MediaColor.FromArgb(0, 96, 96, 96))
        };
    }

    private static RadialGradientBrush CreateLampBrush(LampVisualConfig config, bool active)
    {
        // 偏移渐变原点可在每个灯面上形成小的玻璃高光。
        var baseColor = active ? config.ActiveColor : config.DimColor;
        return new RadialGradientBrush
        {
            Center = new WpfPoint(0.42, 0.32),
            GradientOrigin = new WpfPoint(0.34, 0.24),
            RadiusX = 0.74,
            RadiusY = 0.74,
            GradientStops =
            {
                new GradientStop(WithAlpha(baseColor, active ? (byte)255 : (byte)204), 0.0),
                new GradientStop(WithAlpha(baseColor, active ? (byte)221 : (byte)160), 0.42),
                new GradientStop(WithAlpha(baseColor, active ? (byte)136 : (byte)105), 1.0)
            }
        };
    }

    private static RadialGradientBrush CreateWellBrush(LampVisualConfig config, bool active)
    {
        // 灯槽保持深色；对应灯亮起时吸收当前颜色。
        return new RadialGradientBrush
        {
            Center = new WpfPoint(0.5, 0.45),
            GradientOrigin = new WpfPoint(0.45, 0.32),
            RadiusX = 0.72,
            RadiusY = 0.72,
            GradientStops =
            {
                new GradientStop(active ? WithAlpha(config.DimColor, 210) : MediaColor.FromRgb(27, 27, 28), 0.0),
                new GradientStop(MediaColor.FromRgb(23, 23, 24), 0.58),
                new GradientStop(MediaColor.FromRgb(7, 7, 8), 1.0)
            }
        };
    }

    private static MediaColor WithAlpha(MediaColor color, byte alpha)
    {
        return MediaColor.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static DropShadowEffect CreateLampGlow(MediaColor color, double blurRadius, double opacity)
    {
        return new DropShadowEffect
        {
            Color = color,
            BlurRadius = blurRadius,
            ShadowDepth = 0,
            Opacity = opacity
        };
    }

    private static DoubleAnimation CreateDoubleAnimation(double targetValue)
    {
        // 短缓动可避免 hooks 写入新状态时视觉变化过于生硬。
        return new DoubleAnimation
        {
            To = targetValue,
            Duration = LampTransitionDuration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
    }

    private TimeSpan GetLampEffectDuration()
    {
        // 设置中用简短字符串预设保存速度，便于持久化。
        return _settings.LampSpeed.ToLowerInvariant() switch
        {
            "slow" => TimeSpan.FromMilliseconds(1500),
            "fast" => TimeSpan.FromMilliseconds(650),
            _ => TimeSpan.FromSeconds(1)
        };
    }

    private static void StartBreath(Ellipse lamp, DropShadowEffect lampGlow, DropShadowEffect wellGlow, TimeSpan duration)
    {
        // 呼吸效果同时动画化透明度和发光，让红灯有动感且不改变布局。
        var opacityAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0.35,
            Duration = duration,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        var glowAnimation = new DoubleAnimation
        {
            From = 0.9,
            To = 0.38,
            Duration = duration,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        lamp.BeginAnimation(OpacityProperty, opacityAnimation);
        lampGlow.BeginAnimation(DropShadowEffect.OpacityProperty, glowAnimation);
        wellGlow.BeginAnimation(DropShadowEffect.OpacityProperty, glowAnimation.Clone());
    }

    private static void StartReferencePulse(Ellipse lamp, Ellipse ring, TimeSpan duration)
    {
        // 参考脉冲会为非工作状态添加扩散光环。
        var lampAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0.2,
            Duration = duration,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        var ringOpacityAnimation = new DoubleAnimation
        {
            From = 0.42,
            To = 0,
            Duration = duration,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var ringScaleAnimation = new DoubleAnimation
        {
            From = 1,
            To = 1.72,
            Duration = duration,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        lamp.BeginAnimation(OpacityProperty, lampAnimation);
        ring.BeginAnimation(OpacityProperty, ringOpacityAnimation);
        if (ring.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, ringScaleAnimation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, ringScaleAnimation.Clone());
        }
    }

    private static void ResetRingScale(Ellipse ring)
    {
        // 恢复默认变换前先停止之前的脉冲动画。
        if (ring.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = 1;
        scale.ScaleY = 1;
    }

    private void StopAnimations()
    {
        // WPF 动画会持续占用依赖属性，必须显式清除。
        foreach (var lamp in _lamps.Values)
        {
            lamp.BeginAnimation(OpacityProperty, null);
            lamp.Opacity = 1;

            if (lamp.Fill is SolidColorBrush brush)
            {
                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            }

            if (lamp.Effect is DropShadowEffect glow)
            {
                glow.BeginAnimation(DropShadowEffect.OpacityProperty, null);
                glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
            }
        }

        foreach (var well in _wells.Values)
        {
            if (well.Effect is DropShadowEffect glow)
            {
                glow.BeginAnimation(DropShadowEffect.OpacityProperty, null);
                glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
            }
        }

        foreach (var ring in _rings.Values)
        {
            ring.BeginAnimation(OpacityProperty, null);
            ring.Opacity = 0;
            ResetRingScale(ring);
        }
    }

    private void RenderSessionRows(IReadOnlyList<CodexSessionStatus> sessions)
    {
        // 会话行重建成本很低，比对小列表差异更简单。
        SessionListPanel.Children.Clear();
        foreach (var session in sessions)
        {
            SessionListPanel.Children.Add(CreateSessionRow(session));
        }
    }

    private UIElement CreateSessionRow(CodexSessionStatus session)
    {
        // 已完成会话在抽屉中仍可读，但视觉上会弱化。
        var row = new Grid
        {
            Margin = new Thickness(4, 8, 0, 0),
            Opacity = session.State == CodexLightState.Green ? (_settings.Theme == "dark" ? 0.52 : 0.78) : 1
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Ellipse
        {
            Width = 9,
            Height = 9,
            Fill = new SolidColorBrush(GetLampConfig(session.State).ActiveColor),
            Effect = CreateLampGlow(GetLampConfig(session.State).GlowColor, 10, 0.75),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dot, 0);
        row.Children.Add(dot);

        var textPanel = new StackPanel();
        var name = new TextBlock
        {
            Text = GetSessionDisplayName(session),
            Foreground = GetPrimaryTextBrush(),
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var meta = new TextBlock
        {
            Text = $"{GetPathTail(session.WorkingDirectory)} · {FormatAge(session.UpdatedAt)}",
            Foreground = GetSecondaryTextBrush(),
            FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 3, 0, 0)
        };
        textPanel.Children.Add(name);
        textPanel.Children.Add(meta);
        Grid.SetColumn(textPanel, 1);
        row.Children.Add(textPanel);

        var label = new Border
        {
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(GetStateBadgeBackground(session.State)),
            Padding = new Thickness(6, 3, 6, 3),
            Child = new TextBlock
            {
                Text = GetStateText(session.State),
                Foreground = new SolidColorBrush(GetStateBadgeForeground(session.State)),
                FontSize = 10
            }
        };
        Grid.SetColumn(label, 2);
        row.Children.Add(label);
        row.ContextMenu = CreateSessionRowContextMenu(session);

        return row;
    }

    private ContextMenu CreateSessionRowContextMenu(CodexSessionStatus session)
    {
        // 单会话命令会操作工作目录或隐藏对应会话文件。
        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "打开工作目录" };
        openItem.Click += (_, _) => OpenSessionDirectory(session);
        menu.Items.Add(openItem);

        var copyItem = new MenuItem { Header = "复制工作目录" };
        copyItem.Click += (_, _) => CopySessionDirectory(session);
        menu.Items.Add(copyItem);

        var hideItem = new MenuItem { Header = "隐藏此会话" };
        hideItem.Click += (_, _) => HideSession(session);
        menu.Items.Add(hideItem);

        return menu;
    }

    private void SetDrawerOpen(bool open, bool suppressAutoOpen)
    {
        // 用户手动关闭后会短暂抑制下一次自动打开，以尊重用户操作。
        SessionDrawer.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (!open && suppressAutoOpen)
        {
            _drawerSuppressedUntil = DateTimeOffset.Now.AddSeconds(10);
            _drawerAutoCloseTimer.Stop();
        }

        UpdateWindowHeight();
    }

    private static string GetSessionDisplayName(CodexSessionStatus session)
    {
        // 优先使用 hook 提供的名称，否则从工作区推导可读标签。
        if (!string.IsNullOrWhiteSpace(session.DisplayName))
        {
            return session.DisplayName;
        }

        return string.IsNullOrWhiteSpace(session.WorkingDirectory)
            ? "Codex"
            : System.IO.Path.GetFileName(session.WorkingDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
    }

    private static string GetPathTail(string path)
    {
        // 只显示父目录和末级目录，让长工作区路径适配紧凑抽屉。
        if (string.IsNullOrWhiteSpace(path))
        {
            return "-";
        }

        var parent = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
        var leaf = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(parent) ? leaf : $@"{parent}\{leaf}";
    }

    private static string FormatAge(DateTimeOffset updatedAt)
    {
        // 相对时间让行内容易扫读，无需完整时间戳。
        var age = DateTimeOffset.Now - updatedAt;
        if (age.TotalSeconds < 60)
        {
            return $"{Math.Max(0, (int)age.TotalSeconds)}s";
        }

        if (age.TotalMinutes < 60)
        {
            return $"{(int)age.TotalMinutes}m";
        }

        return $"{(int)age.TotalHours}h";
    }

    private static string GetStateText(CodexLightState state)
    {
        return state switch
        {
            CodexLightState.Yellow => "等权限",
            CodexLightState.Red => "处理中",
            CodexLightState.Green => "完成",
            _ => "未知"
        };
    }

    private MediaColor GetStateBadgeBackground(CodexLightState state)
    {
        if (_settings.Theme != "dark")
        {
            return state switch
            {
                CodexLightState.Yellow => MediaColor.FromArgb(46, 255, 159, 10),
                CodexLightState.Red => MediaColor.FromArgb(42, 255, 59, 48),
                CodexLightState.Green => MediaColor.FromArgb(46, 48, 209, 88),
                _ => MediaColor.FromRgb(235, 236, 240)
            };
        }

        return state switch
        {
            CodexLightState.Yellow => MediaColor.FromArgb(38, 255, 159, 10),
            CodexLightState.Red => MediaColor.FromArgb(36, 255, 59, 48),
            CodexLightState.Green => MediaColor.FromArgb(34, 48, 209, 88),
            _ => MediaColor.FromRgb(43, 44, 49)
        };
    }

    private MediaColor GetStateBadgeForeground(CodexLightState state)
    {
        if (_settings.Theme != "dark")
        {
            return state switch
            {
                CodexLightState.Yellow => MediaColor.FromRgb(126, 78, 0),
                CodexLightState.Red => MediaColor.FromRgb(138, 37, 32),
                CodexLightState.Green => MediaColor.FromRgb(21, 105, 48),
                _ => MediaColor.FromRgb(65, 68, 76)
            };
        }

        return state switch
        {
            CodexLightState.Yellow => MediaColor.FromRgb(255, 211, 144),
            CodexLightState.Red => MediaColor.FromRgb(255, 177, 173),
            CodexLightState.Green => MediaColor.FromRgb(156, 243, 182),
            _ => MediaColor.FromRgb(216, 216, 220)
        };
    }

    private void CreateTrayMenu()
    {
        // 使用 Windows Forms 托盘图标，因为 WPF 没有内置通知图标。
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Codex 红绿灯",
            Visible = true,
            Icon = LoadApplicationIcon(),
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };

        RebuildTrayMenu();
    }

    private void RebuildTrayMenu()
    {
        // 设置变化后重建菜单文字，使可切换状态反映在文本中。
        if (_notifyIcon?.ContextMenuStrip is null)
        {
            return;
        }

        var menu = _notifyIcon.ContextMenuStrip;
        menu.Items.Clear();
        menu.Items.Add("显示/隐藏红绿灯", null, (_, _) => ToggleWindowVisibility());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("切换到红灯", null, (_, _) => SetManualState(CodexLightState.Red, "manual"));
        menu.Items.Add("切换到黄灯", null, (_, _) => SetManualState(CodexLightState.Yellow, "manual"));
        menu.Items.Add("切换到绿灯", null, (_, _) => SetManualState(CodexLightState.Green, "manual"));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_settings.Style == "triple" ? "切换到单灯样式" : "切换到三灯样式", null, (_, _) => ToggleStyle());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("查看配置路径", null, (_, _) => WpfMessageBox.Show(_paths.HooksPath, "Codex hooks 配置路径"));
        menu.Items.Add("重新写入配置", null, (_, _) => RewriteHooks(showReminder: true));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_settings.Theme == "dark" ? "切换浅色模式" : "切换深色模式", null, (_, _) => ToggleTheme());
        menu.Items.Add(_settings.AutoLaunchOnCodexActivity ? "关闭 Codex 自动打开红绿灯" : "开启 Codex 自动打开红绿灯", null, (_, _) => ToggleAutoLaunchOnCodexActivity());
        menu.Items.Add(_settings.StartWithWindows ? "关闭开机自动启动" : "开启开机自动启动", null, (_, _) => ToggleStartWithWindows());
        menu.Items.Add(_settings.AutoOpenDrawerOnYellow ? "关闭黄灯自动展开" : "开启黄灯自动展开", null, (_, _) => ToggleAutoOpenDrawerOnYellow());
        menu.Items.Add(_settings.ShowEndedSessions ? "隐藏已结束会话" : "显示已结束会话", null, (_, _) => ToggleShowEndedSessions());
        menu.Items.Add("清理已完成会话", null, (_, _) => ClearEndedSessions());
        menu.Items.Add("统计", null, (_, _) => ShowStatsWindow());
        menu.Items.Add("诊断", null, (_, _) => ShowDiagnosticsWindow());
        menu.Items.Add("检查更新", null, async (_, _) => await CheckForUpdatesAsync());
        menu.Items.Add("关于我", null, (_, _) => ShowAbout());
        menu.Items.Add("设置", null, (_, _) => ShowSettingsWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => System.Windows.Application.Current.Shutdown());
    }

    private static void ShowAbout()
    {
        WpfMessageBox.Show(AboutText, "关于");
    }

    private void ToggleWindowVisibility()
    {
        // 托盘命令会恢复紧凑悬浮窗口，而不是创建新窗口。
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
            Activate();
        }
    }

    private void ShowSettingsWindow()
    {
        // 对话框持有一份设置副本，仅在用户保存后返回。
        var window = new SettingsWindow(_settings)
        {
            Owner = this
        };

        if (window.ShowDialog() != true)
        {
            return;
        }

        ApplySettings(window.Settings);
    }

    private void ShowYellowReminderIfNeeded(CodexLightState state)
    {
        // 每次切换到黄灯只显示一次通知，静音时除外。
        if (state != CodexLightState.Yellow)
        {
            _lastReminderState = state;
            return;
        }

        if (_lastReminderState == CodexLightState.Yellow)
        {
            return;
        }

        _lastReminderState = state;
        if (_settings.Muted || _notifyIcon is null)
        {
            return;
        }

        var reminderMode = _settings.ReminderMode.ToLowerInvariant();
        if (reminderMode is "silent" or "flash")
        {
            return;
        }

        if (reminderMode == "balloon-sound")
        {
            SystemSounds.Exclamation.Play();
        }

        _notifyIcon.BalloonTipTitle = "Codex 等待权限";
        _notifyIcon.BalloonTipText = "有任务需要你确认权限。";
        _notifyIcon.ShowBalloonTip(3000);
    }

    private void ApplySettings(AppSettings settings)
    {
        // 设置可能影响 hooks、布局、主题和过滤，因此逐项刷新相关界面。
        SaveSettings(settings);
        StartupRegistrationService.SetEnabled(_settings.StartWithWindows);
        RewriteHooks(showReminder: false);
        Topmost = _settings.Topmost;
        MuteCheckBox.IsChecked = _settings.Muted;
        ThemeCheckBox.IsChecked = _settings.Theme == "dark";
        ApplyTheme(_settings.Theme);
        ApplyState(_currentState);
        ApplySessions(LoadCurrentSessions());
        RebuildTrayMenu();
    }

    private void SetManualState(CodexLightState state, string eventName)
    {
        // 托盘手动命令写入与 hook 事件相同的状态文件。
        var status = new CodexStatus(state, eventName, DateTimeOffset.Now);
        _statusStore.Write(status);
        ApplyStatus(status);
    }

    private void RewriteHooks(bool showReminder)
    {
        // 重新安装是幂等的，只替换本应用拥有的配置项。
        var path = new CodexHookInstaller(_paths).InstallOrUpdate();
        if (showReminder)
        {
            ShowHookTrustReminder(path);
        }
    }

    private void InstallHooksAtStartup()
    {
        // 启动时确保 hooks 存在，并在首次启动时创建初始绿灯状态。
        var hooksPath = new CodexHookInstaller(_paths).InstallOrUpdate();
        if (!File.Exists(_paths.StatusPath))
        {
            _statusStore.Write(CodexLightState.Green, "app-start");
            ApplyState(CodexLightState.Green);
        }

        if (_shouldShowTrustReminder)
        {
            ShowHookTrustReminder(hooksPath);
        }
    }

    private static void ShowHookTrustReminder(string hooksPath)
    {
        WpfMessageBox.Show(
            $"Codex hooks 已写入：{hooksPath}\n\n请在 Codex 中运行 /hooks，并信任 Codex 红绿灯 hooks。未信任前，Codex 会跳过这些 hook。",
            "Codex 红绿灯");
    }

    private void ToggleStyle()
    {
        // 样式可在三灯显示和单个活动灯显示之间切换。
        var next = _settings.Style == "triple" ? "single" : "triple";
        SaveSettings(_settings with { Style = next });
        ApplyStyle(next);
        RebuildTrayMenu();
    }

    private void ApplyStyle(string style)
    {
        // 单灯模式下未知状态保留黄灯可见，避免外壳为空。
        if (style == "single")
        {
            Body.Height = 92;
            SetHostVisibility(CodexLightState.Red, _currentState == CodexLightState.Red);
            SetHostVisibility(CodexLightState.Yellow, _currentState is CodexLightState.Yellow or CodexLightState.Unknown);
            SetHostVisibility(CodexLightState.Green, _currentState == CodexLightState.Green);
            UpdateWindowHeight();
            return;
        }

        Body.Height = 200;
        SetHostVisibility(CodexLightState.Red, true);
        SetHostVisibility(CodexLightState.Yellow, true);
        SetHostVisibility(CodexLightState.Green, true);
        UpdateWindowHeight();
    }

    private void SetHostVisibility(CodexLightState state, bool visible)
    {
        _lampHosts[state].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleTheme()
    {
        // 主题变化会立即持久化，并同步到托盘菜单文字。
        var next = _settings.Theme == "dark" ? "light" : "dark";
        SaveSettings(_settings with { Theme = next });
        ApplyTheme(next);
        RebuildTrayMenu();
    }

    private void ToggleAutoOpenDrawerOnYellow()
    {
        SaveSettings(_settings with { AutoOpenDrawerOnYellow = !_settings.AutoOpenDrawerOnYellow });
        RebuildTrayMenu();
    }

    private void ToggleAutoLaunchOnCodexActivity()
    {
        SaveSettings(_settings with { AutoLaunchOnCodexActivity = !_settings.AutoLaunchOnCodexActivity });
        RewriteHooks(showReminder: false);
        RebuildTrayMenu();
    }

    private void ToggleStartWithWindows()
    {
        var next = !_settings.StartWithWindows;
        SaveSettings(_settings with { StartWithWindows = next });
        StartupRegistrationService.SetEnabled(next);
        RebuildTrayMenu();
    }

    private void ToggleShowEndedSessions()
    {
        // 该设置会改变会话可见性，因此需要刷新抽屉。
        SaveSettings(_settings with { ShowEndedSessions = !_settings.ShowEndedSessions });
        RebuildTrayMenu();
        ApplySessions(LoadCurrentSessions());
    }

    private void ClearEndedSessions()
    {
        // 只删除已过期的完成会话，活动文件保持不变。
        _sessionStatusStore.ClearEndedSessions(_settings.ToSessionRetentionOptions());
        ApplySessions(LoadCurrentSessions());
    }

    private void OpenSessionDirectory(CodexSessionStatus session)
    {
        // 使用 shell 执行，让资源管理器按 Windows 默认行为打开文件夹。
        if (string.IsNullOrWhiteSpace(session.WorkingDirectory) || !Directory.Exists(session.WorkingDirectory))
        {
            WpfMessageBox.Show("工作目录不存在。", "Codex 任务");
            return;
        }

        Process.Start(new ProcessStartInfo(session.WorkingDirectory) { UseShellExecute = true });
    }

    private static void CopySessionDirectory(CodexSessionStatus session)
    {
        if (!string.IsNullOrWhiteSpace(session.WorkingDirectory))
        {
            System.Windows.Clipboard.SetText(session.WorkingDirectory);
        }
    }

    private void HideSession(CodexSessionStatus session)
    {
        // 隐藏会话通过删除对应的单会话 JSON 文件实现。
        _sessionStatusStore.DeleteSession(session.SessionId);
        ApplySessions(LoadCurrentSessions());
    }

    private IReadOnlyList<CodexSessionStatus> LoadCurrentSessions()
    {
        return _sessionStatusStore.LoadSessions(_settings.ShowEndedSessions, _settings.ToSessionRetentionOptions());
    }

    private void ApplyTheme(string theme)
    {
        // 应用较小，没有资源主题层，因此直接应用主题颜色。
        Body.Background = theme == "dark"
            ? new SolidColorBrush(MediaColor.FromRgb(43, 43, 43))
            : new SolidColorBrush(MediaColor.FromRgb(230, 230, 230));
        SettingsPanel.Background = theme == "dark"
            ? new SolidColorBrush(MediaColor.FromRgb(42, 42, 42))
            : new SolidColorBrush(MediaColor.FromRgb(238, 238, 238));
        SessionDrawer.Background = GetDrawerBackgroundBrush();
        SessionDrawerTitle.Foreground = GetPrimaryTextBrush();
        SessionDrawerSortLabel.Foreground = GetSecondaryTextBrush();
        SessionDrawerDivider.Background = theme == "dark"
            ? new SolidColorBrush(MediaColor.FromArgb(34, 255, 255, 255))
            : new SolidColorBrush(MediaColor.FromArgb(38, 24, 27, 31));
        SessionDrawerAccent.Background = new SolidColorBrush(MediaColor.FromRgb(255, 159, 10));
        MuteCheckBox.Foreground = GetSettingsTextBrush();
        ThemeCheckBox.Foreground = GetSettingsTextBrush();
        RenderSessionRows(_visibleSessions);
    }

    private SolidColorBrush GetDrawerBackgroundBrush()
    {
        return _settings.Theme == "dark"
            ? new SolidColorBrush(MediaColor.FromArgb(247, 32, 33, 36))
            : new SolidColorBrush(MediaColor.FromArgb(246, 246, 247, 250));
    }

    private SolidColorBrush GetPrimaryTextBrush()
    {
        return _settings.Theme == "dark"
            ? new SolidColorBrush(MediaColor.FromRgb(246, 246, 248))
            : new SolidColorBrush(MediaColor.FromRgb(31, 34, 40));
    }

    private SolidColorBrush GetSecondaryTextBrush()
    {
        return _settings.Theme == "dark"
            ? new SolidColorBrush(MediaColor.FromRgb(168, 168, 176))
            : new SolidColorBrush(MediaColor.FromRgb(98, 104, 116));
    }

    private SolidColorBrush GetSettingsTextBrush()
    {
        return _settings.Theme == "dark"
            ? new SolidColorBrush(MediaColor.FromRgb(246, 246, 248))
            : new SolidColorBrush(MediaColor.FromRgb(55, 58, 66));
    }

    private static DrawingIcon LoadApplicationIcon()
    {
        // 优先使用打包后的可执行文件图标，失败时使用默认应用图标。
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            return DrawingIcon.ExtractAssociatedIcon(processPath) ?? DrawingSystemIcons.Application;
        }

        return DrawingSystemIcons.Application;
    }

    private void ShowStatsWindow()
    {
        // 打开统计窗口时计算摘要，确保反映最新统计文件。
        var window = new StatsWindow(_statsStore.GetTodaySummary(), _statsStore.GetCurrentWeekSummary())
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void ShowDiagnosticsWindow()
    {
        var window = new DiagnosticsWindow(BuildDiagnosticsText())
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private string BuildDiagnosticsText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Codex 红绿灯诊断");
        builder.AppendLine($"版本: {GetCurrentVersion()}");
        builder.AppendLine($"进程路径: {Environment.ProcessPath}");
        builder.AppendLine($"当前灯: {_currentState}");
        builder.AppendLine($"托盘静音: {_settings.Muted}");
        builder.AppendLine($"提醒模式: {_settings.ReminderMode}");
        builder.AppendLine($"开机启动设置: {_settings.StartWithWindows}");
        builder.AppendLine($"开机启动注册: {StartupRegistrationService.IsEnabled()}");
        builder.AppendLine($"Codex 活动自动打开: {_settings.AutoLaunchOnCodexActivity}");
        builder.AppendLine($"黄灯自动展开: {_settings.AutoOpenDrawerOnYellow}");
        builder.AppendLine($"显示已结束会话: {_settings.ShowEndedSessions}");
        builder.AppendLine($"会话数量: {_visibleSessions.Count}");
        builder.AppendLine();
        builder.AppendLine("路径");
        builder.AppendLine($"Codex 目录: {_paths.CodexDirectory}");
        builder.AppendLine($"Hooks 配置: {_paths.HooksPath}");
        builder.AppendLine($"状态文件: {_paths.StatusPath}");
        builder.AppendLine($"设置文件: {_paths.SettingsPath}");
        builder.AppendLine($"统计文件: {_paths.StatsPath}");
        builder.AppendLine($"Hook 脚本: {_paths.HookScriptPath}");
        builder.AppendLine($"会话目录: {_paths.SessionDirectory}");
        builder.AppendLine($"诊断目录: {_paths.HookDiagnosticsDirectory}");
        builder.AppendLine();
        builder.AppendLine("保留时间");
        builder.AppendLine($"绿灯: {_settings.GreenRetentionMinutes} 分钟");
        builder.AppendLine($"黄灯: {_settings.YellowRetentionMinutes} 分钟");
        builder.AppendLine($"红灯: {_settings.RedRetentionMinutes} 分钟");
        builder.AppendLine($"CLI 运行中: {_settings.LiveCliWorkRetentionHours} 小时");
        builder.AppendLine($"VS Code 插件运行中: {_settings.LiveVsCodePluginWorkRetentionHours} 小时");

        var latestDiagnosticsPath = System.IO.Path.Combine(_paths.HookDiagnosticsDirectory, "latest-hook-context.json");
        builder.AppendLine();
        builder.AppendLine("最近 hook 诊断");
        if (File.Exists(latestDiagnosticsPath))
        {
            builder.AppendLine(File.ReadAllText(latestDiagnosticsPath));
        }
        else
        {
            builder.AppendLine("未找到 latest-hook-context.json");
        }

        return builder.ToString();
    }

    private async Task CheckForUpdatesAsync()
    {
        // HTTP 检查使用独立超时运行，同时保持界面响应。
        var menu = _notifyIcon?.ContextMenuStrip;
        var previousCursor = Cursor;
        Cursor = System.Windows.Input.Cursors.Wait;
        try
        {
            using var httpClient = new HttpClient();
            var checker = new UpdateChecker(httpClient);
            var result = await checker.CheckAsync(GetCurrentVersion(), UpdateManifestUrl, TimeSpan.FromSeconds(5));

            if (!result.IsSuccess)
            {
                WpfMessageBox.Show(result.Message, "检查更新");
                return;
            }

            if (!result.HasUpdate)
            {
                WpfMessageBox.Show($"当前已是最新版本：{result.CurrentVersion}", "检查更新");
                return;
            }

            var notes = result.Notes.Count == 0
                ? "暂无更新说明。"
                : string.Join("\n", result.Notes.Select(note => "- " + note));
            var message = $"发现新版本：{result.LatestVersion}\n\n更新内容：\n{notes}\n\n是否打开 GitHub 下载页面？";
            if (WpfMessageBox.Show(message, "检查更新", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                OpenUrl(result.DownloadUrl);
            }
        }
        finally
        {
            Cursor = previousCursor;
            menu?.Focus();
        }
    }

    private static string GetCurrentVersion()
    {
        // 程序集版本来自项目文件；兜底值用于处理调试异常情况。
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.3";
    }

    private static void OpenUrl(string url)
    {
        // shell 执行会把 HTTPS 打开交给用户默认浏览器。
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void ApplySavedWindowPosition()
    {
        // 恢复上次窗口位置；没有记录时默认放在工作区右上附近。
        if (_settings.WindowLeft.HasValue && _settings.WindowTop.HasValue)
        {
            Left = _settings.WindowLeft.Value;
            Top = _settings.WindowTop.Value;
            return;
        }

        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 80;
    }

    private void SaveSettings(AppSettings settings)
    {
        // 持久化前先更新内存快照，保证后续界面读取一致。
        _settings = settings;
        _settingsStore?.Save(_settings);
    }

    private void UpdateWindowHeight()
    {
        // 宽高按固定格式计算，避免托盘尺寸界面随内容漂移。
        var panelOpen = SettingsPanel.Visibility == Visibility.Visible;
        Width = SessionDrawer.Visibility == Visibility.Visible ? 374 : 100;
        var baseHeight = _settings.Style == "single"
            ? (panelOpen ? 192 : 112)
            : (panelOpen ? 298 : 220);
        Height = SessionDrawer.Visibility == Visibility.Visible
            ? Math.Max(baseHeight, 216)
            : baseHeight;
    }

    private void Body_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 无边框窗口使用主体区域作为拖拽把手。
        DragMove();
    }

    private void GearButton_Click(object sender, RoutedEventArgs e)
    {
        // 存在多个会话时，齿轮按钮优先切换抽屉而不是设置面板。
        if (_visibleSessions.Count > 1)
        {
            SetDrawerOpen(SessionDrawer.Visibility != Visibility.Visible, suppressAutoOpen: true);
            return;
        }

        SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        UpdateWindowHeight();
    }

    private void SessionCountBadge_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 防止点击徽标时同时触发窗口拖拽处理。
        e.Handled = true;
        if (_visibleSessions.Count > 1)
        {
            SetDrawerOpen(SessionDrawer.Visibility != Visibility.Visible, suppressAutoOpen: true);
        }
    }

    private void MuteCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // 内联复选框变化会立即持久化。
        SaveSettings(_settings with { Muted = MuteCheckBox.IsChecked == true });
    }

    private void ThemeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // 紧凑面板与托盘主题命令保持一致。
        var theme = ThemeCheckBox.IsChecked == true ? "dark" : "light";
        SaveSettings(_settings with { Theme = theme });
        ApplyTheme(theme);
        RebuildTrayMenu();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        // 用户拖动悬浮窗口后保存位置。
        base.OnLocationChanged(e);
        _settingsStore?.Save(_settings with { WindowLeft = Left, WindowTop = Top });
    }

    protected override void OnClosed(EventArgs e)
    {
        // 清理监听器和托盘资源，确保单实例互斥体能干净释放。
        _watcher.Dispose();
        _sessionWatcher.Dispose();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.OnClosed(e);
    }

    private sealed record LampVisualConfig(
        MediaColor ActiveColor,
        MediaColor DimColor,
        MediaColor GlowColor,
        MediaColor InnerGlowColor);
}
