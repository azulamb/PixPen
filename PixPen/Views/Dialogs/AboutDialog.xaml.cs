using System.Reflection;
using System.Windows;

namespace PixPen.Views.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly()
            .GetName().Version;
        VersionText.Text = version != null
            ? $"PixPen v{version.Major}.{version.Minor}.{version.Build}"
            : "PixPen";
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
