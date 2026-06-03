using System.ComponentModel;
using System.Windows;

namespace PixPen.Views;

public partial class FloatingPanelWindow : Window
{
    public string PanelName { get; }
    public event EventHandler<string>? DockRequested;

    private bool _forceClose;

    public FloatingPanelWindow(string panelName, string panelTitle, UIElement content)
    {
        InitializeComponent();
        PanelName = panelName;
        TitleText.Text = panelTitle;
        Title = panelTitle;
        PanelHost.Content = content;
    }

    /// <summary>パネルをウィンドウから切り離す（ドッキング前に呼ぶ）</summary>
    public void DetachPanel()
    {
        PanelHost.Content = null;
    }

    /// <summary>DockRequested を発火せずウィンドウを閉じる</summary>
    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void OnDock(object sender, RoutedEventArgs e)
    {
        DockRequested?.Invoke(this, PanelName);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_forceClose) return;
        // X ボタンはドッキングとして扱う
        e.Cancel = true;
        DockRequested?.Invoke(this, PanelName);
    }
}
