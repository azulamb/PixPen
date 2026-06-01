using System.Windows;
using PixPen.ViewModels;

namespace PixPen.Views.Dialogs;

public partial class DocumentSettingsDialog : Window
{
    public DocumentSettingsDialog() => InitializeComponent();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
