using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
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

    // ─── ドラッグ＆ドロップ ────────────────────────────────────────────────

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        // .ppx ファイルが含まれる場合のみ受け入れる
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Any(f =>
                string.Equals(System.IO.Path.GetExtension(f), ".ppx",
                    StringComparison.OrdinalIgnoreCase)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (files == null) return;

        foreach (var file in files.Where(f =>
            string.Equals(System.IO.Path.GetExtension(f), ".ppx",
                StringComparison.OrdinalIgnoreCase)))
        {
            _vm?.OpenFile(file);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // 未保存のタブがあれば確認ダイアログを表示する
        if (_vm != null && _vm.Tabs.Any(t => t.Document.IsModified))
        {
            var dlg = new Views.Dialogs.UnsavedChangesDialog { Owner = this };
            var result = dlg.ShowDialog();

            // 「戻る」またはバツボタン（result != true）→ 閉じるをキャンセル
            if (result != true)
            {
                e.Cancel = true;
                return;
            }
            // 「保存せず終了」（result == true）→ そのまま閉じる
        }
        base.OnClosing(e);
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
