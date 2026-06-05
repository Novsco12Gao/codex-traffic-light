using System.Windows;

namespace CodexTrafficLight.App;

public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow(string diagnosticsText)
    {
        InitializeComponent();
        DiagnosticsTextBox.Text = diagnosticsText;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(DiagnosticsTextBox.Text);
    }
}
