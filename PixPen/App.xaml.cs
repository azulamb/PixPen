using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PixPen.Services;

namespace PixPen;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ログファイルを起動時に上書き初期化
        Logger.Initialize();
        Logger.Info("Application started");

        // UI スレッドの未処理例外
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        // バックグラウンドスレッドの未処理例外
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        // Task の未観測例外
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("Application exited");
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("Unhandled UI exception", e.Exception);
        // クラッシュを防いで続行（必要に応じて e.Handled = true のまま）
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Logger.Error("Unhandled domain exception", e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error("Unobserved task exception", e.Exception);
        e.SetObserved();
    }

    // ─── グローバルスタイラス状態（WPF StylusDown バックアップ用）──────────
    public static double LastStylusPressure { get; private set; } = 1.0;
    public static bool   LastStylusIsEraser { get; private set; } = false;
    public static bool   StylusIsDown       { get; private set; } = false;

    static App()
    {
        // Cintiq など WM_POINTER ベースのデバイス向け（将来の互換性のため維持）
        AppContext.SetSwitch("Switch.System.Windows.Input.Stylus.EnablePointerSupport", true);

        EventManager.RegisterClassHandler(typeof(UIElement), Stylus.StylusDownEvent,
            new StylusDownEventHandler(OnGlobalStylusDown), handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(UIElement), Stylus.StylusMoveEvent,
            new StylusEventHandler(OnGlobalStylusMove), handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(UIElement), Stylus.StylusUpEvent,
            new StylusEventHandler(OnGlobalStylusUp), handledEventsToo: true);
    }

    private static void OnGlobalStylusDown(object sender, StylusDownEventArgs e) { UpdateStylusState(e); StylusIsDown = true; }
    private static void OnGlobalStylusMove(object sender, StylusEventArgs e) => UpdateStylusState(e);
    private static void OnGlobalStylusUp(object sender, StylusEventArgs e) => StylusIsDown = false;

    private static void UpdateStylusState(StylusEventArgs e)
    {
        try
        {
            var pts = e.StylusDevice.GetStylusPoints(null);
            if (pts.Count > 0)
            {
                LastStylusPressure = Math.Clamp((double)pts[pts.Count - 1].PressureFactor, 0.0, 1.0);
                LastStylusIsEraser = e.StylusDevice.Inverted;
            }
        }
        catch { }
    }
}
