using System.Windows;

namespace PixPen.Views.Dialogs;

public partial class GridSettingsDialog : Window
{
    public GridSettingsDialog() => InitializeComponent();
    private void OnOk(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
