using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace PixPen.Views.Dialogs;

public partial class AboutDialog : Window
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private const string VersionJsonUrl  = "https://azulamb.github.io/PixPen/version.json";
    private const string ReleasesPageUrl = "https://github.com/azulamb/PixPen/releases";

    private readonly Version? _currentVersion;

    public AboutDialog()
    {
        InitializeComponent();

        _currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = _currentVersion != null
            ? $"PixPen v{_currentVersion.Major}.{_currentVersion.Minor}.{_currentVersion.Build}"
            : "PixPen";
    }

    // ─── 最新バージョン確認 ────────────────────────────────────────────────

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text       = "最新バージョンを確認中...";
        UpdateStatusText.Foreground = FindResource("App.Subtle.Fg") as System.Windows.Media.Brush;

        try
        {
            var json    = await _http.GetStringAsync(VersionJsonUrl);
            var doc     = JsonDocument.Parse(json);
            var verStr  = doc.RootElement.GetProperty("version").GetString() ?? "";
            var latest  = Version.TryParse(verStr, out var v) ? v : null;

            if (latest == null)
            {
                SetStatus("バージョン情報を取得できませんでした。", isError: true);
            }
            else if (_currentVersion != null && latest > _currentVersion)
            {
                SetStatus($"新しいバージョン v{latest.Major}.{latest.Minor}.{latest.Build} が利用可能です。",
                          isError: false, isNew: true);
            }
            else
            {
                SetStatus("最新バージョンを使用しています。", isError: false);
            }
        }
        catch
        {
            SetStatus("確認失敗。ネットワークを確認してください。", isError: true);
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void SetStatus(string message, bool isError, bool isNew = false)
    {
        UpdateStatusText.Text = message;
        UpdateStatusText.Foreground = (isError ? FindResource("App.Fg") : isNew
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x30, 0xA0, 0x58))
            : FindResource("App.Subtle.Fg")) as System.Windows.Media.Brush;
    }

    // ─── リリースページを開く ──────────────────────────────────────────────

    private void OnOpenReleasePage(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(ReleasesPageUrl) { UseShellExecute = true });
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
