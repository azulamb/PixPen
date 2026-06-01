namespace PixPen.Services;

/// <summary>
/// WPF 標準 Stylus API (Windows Ink) によるタブレットサービス。
/// DrawingCanvas が直接 StylusDown/Move/Up イベントを処理するため、
/// このサービスは「利用可能フラグ」と表示名の提供のみ担当する。
/// </summary>
public class WindowsInkTabletService : ITabletService
{
    public bool IsAvailable => true;
    public string DisplayName => "Windows Ink (標準)";

    public void Initialize(System.Windows.Window window) { }

    public void Dispose() { }
}
