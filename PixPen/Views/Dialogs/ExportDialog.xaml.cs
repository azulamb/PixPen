using System.Windows;
using Microsoft.Win32;
using PixPen.Services;
using PixPen.ViewModels;

namespace PixPen.Views.Dialogs;

public partial class ExportDialog : Window
{
    private readonly FileService _fileService = new();

    public ExportDialog() => InitializeComponent();

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CanvasTabViewModel tab) return;

        if (RadioMerged.IsChecked == true)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "PNG 画像 (*.png)|*.png",
                FileName = tab.Document.Title,
                Title = "エクスポート（合成）"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _fileService.ExportMerged(tab.Document, dlg.FileName);
                MessageBox.Show("エクスポートが完了しました。", "完了",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エクスポートに失敗しました:\n{ex.Message}", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            // WPF ネイティブのフォルダ選択ダイアログ（.NET 8+）
            var dlg = new OpenFolderDialog
            {
                Title = "保存先フォルダを選択してください"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _fileService.ExportLayersToFolder(tab.Document, dlg.FolderName);
                MessageBox.Show("エクスポートが完了しました。", "完了",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エクスポートに失敗しました:\n{ex.Message}", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
