using System.Windows;
using PixPen.Models;

namespace PixPen.Views.Dialogs;

public partial class TabletSettingsDialog : Window
{
    public TabletSettingsDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
