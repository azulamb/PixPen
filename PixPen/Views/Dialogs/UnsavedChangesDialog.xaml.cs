using System.Windows;

namespace PixPen.Views.Dialogs;

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog() => InitializeComponent();

    // 「保存せず終了」→ DialogResult = true（終了を許可）
    private void OnExitWithoutSave(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    // 「戻る」→ DialogResult = false（終了をキャンセル）
    // IsCancel="True" のボタンは Escape でも呼ばれる
    private void OnReturn(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    // バツボタン（ウィンドウ閉じる）→ DialogResult を設定しないため null が返る
    // ShowDialog() != true → 終了キャンセル扱い（「戻る」と同じ）
}
