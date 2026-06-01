using System.Windows;
using System.Windows.Interop;
using PixPen.ViewModels;

namespace PixPen;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // アイコンをコードビハインドで設定（XAML のパス大文字小文字問題を回避）
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/PixPen.ico");
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(uri);
        }
        catch { }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (_vm?.ActiveTab?.TabletService is Services.WinTabTabletService wt)
            wt.Initialize(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_vm != null)
        {
            foreach (var tab in _vm.Tabs)
                tab.TabletService?.Dispose();
        }
        base.OnClosed(e);
    }
}
