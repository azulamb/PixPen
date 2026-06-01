using System.Windows;

namespace PixPen.Views.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog() => InitializeComponent();
    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
