using System.Windows;

namespace CodexTrafficLight.App;

/// <summary>
/// 显示当前程序、路径、设置和 hook 快照等诊断信息的窗口。
/// 这些信息主要用于用户把问题现场复制给维护者排查。
/// </summary>
public partial class DiagnosticsWindow : Window
{
    /// <summary>
    /// 创建诊断窗口，并把主窗口整理好的诊断文本放入只读文本框。
    /// </summary>
    public DiagnosticsWindow(string diagnosticsText)
    {
        InitializeComponent();
        DiagnosticsTextBox.Text = diagnosticsText;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        // 明确使用 WPF 剪贴板，避免和 Windows Forms 的同名类型冲突。
        System.Windows.Clipboard.SetText(DiagnosticsTextBox.Text);
    }
}
