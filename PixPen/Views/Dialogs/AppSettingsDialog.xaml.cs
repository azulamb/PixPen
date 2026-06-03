using System.Windows;
using PixPen.Models;
using PixPen.Services;

namespace PixPen.Views.Dialogs;

public partial class AppSettingsDialog : Window
{
    private readonly AppSettings _settings;

    public AppSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        DataContext = settings;

        // テーマ ComboBox を初期化（0=System, 1=Light, 2=Dark）
        ComboTheme.SelectedIndex = settings.Theme switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark  => 2,
            _              => 0
        };
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        // テーマ設定を反映
        var newTheme = ComboTheme.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System
        };

        bool themeChanged = _settings.Theme != newTheme;
        _settings.Theme = newTheme;

        if (themeChanged)
            ThemeService.SetMode(newTheme);

        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
