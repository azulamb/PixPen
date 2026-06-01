using System.Windows;
using PixPen.Models;

namespace PixPen.Views.Dialogs;

public partial class AppSettingsDialog : Window
{
    public AppSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        DataContext = settings;
    }

    private void OnOk(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
