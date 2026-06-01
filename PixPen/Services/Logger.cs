using System.IO;

namespace PixPen.Services;

/// <summary>
/// シンプルなファイルロガー。
/// 起動時に上書き初期化し、エラー・情報を追記する。
/// ログは %AppData%\PixPen\error.log に出力される。
/// </summary>
public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PixPen", "error.log");

    private static readonly object _lock = new();

    /// <summary>ログファイルを初期化する（起動時に上書き）</summary>
    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.WriteAllText(LogPath,
                $"=== PixPen Log [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ==={Environment.NewLine}");
            Info($"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            Info($"OS: {Environment.OSVersion}");
            Info($".NET: {Environment.Version}");
        }
        catch { }
    }

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            lock (_lock)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {level}: {message}");
                if (ex != null)
                {
                    sb.AppendLine($"  Type   : {ex.GetType().FullName}");
                    sb.AppendLine($"  Message: {ex.Message}");
                    if (ex.InnerException != null)
                        sb.AppendLine($"  Inner  : {ex.InnerException.Message}");
                    sb.AppendLine($"  Stack  :{Environment.NewLine}{ex.StackTrace}");
                }
                File.AppendAllText(LogPath, sb.ToString());
            }
        }
        catch { }
    }

    public static string LogFilePath => LogPath;
}
