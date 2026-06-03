using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using PixPen.Models;

namespace PixPen.Services;

public static class ThemeService
{
    // Windows 10 1903+ / Windows 11 のタイトルバーダークモード API
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private static AppTheme _mode = AppTheme.System;

    /// <summary>起動時に一度だけ呼ぶ。システムテーマ変化の監視も開始する。</summary>
    public static void Initialize(AppTheme mode)
    {
        _mode = mode;

        // 以降に生成されるすべての Window に自動適用
        EventManager.RegisterClassHandler(
            typeof(Window), Window.LoadedEvent,
            new RoutedEventHandler((s, _) => { if (s is Window w) ApplyDwm(w); }));

        // システムテーマ変化を監視（System 追従モードのとき）
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && _mode == AppTheme.System)
                Application.Current.Dispatcher.BeginInvoke(Apply);
        };

        Apply();
    }

    /// <summary>設定変更時に呼ぶ。</summary>
    public static void SetMode(AppTheme mode)
    {
        _mode = mode;
        Apply();
    }

    // ─── 内部 ────────────────────────────────────────────────────────────

    private static bool IsDark =>
        _mode == AppTheme.Dark ||
        (_mode == AppTheme.System && IsSystemDark());

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    private static void Apply()
    {
        bool dark = IsDark;
        var uri  = new Uri(dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };

        // index 0 のカラーテーマ辞書を差し替える
        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 0)
            merged[0] = dict;
        else
            merged.Insert(0, dict);

        // 現在開いているすべての Window のタイトルバーを更新
        foreach (Window win in Application.Current.Windows)
            ApplyDwm(win, dark);
    }

    /// <summary>指定 Window のタイトルバーにダークモードを適用する。</summary>
    public static void ApplyDwm(Window window, bool? forceDark = null)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) return;
        int val = (forceDark ?? IsDark) ? 1 : 0;
        DwmSetWindowAttribute(helper.Handle,
            DWMWA_USE_IMMERSIVE_DARK_MODE, ref val, sizeof(int));
    }
}
