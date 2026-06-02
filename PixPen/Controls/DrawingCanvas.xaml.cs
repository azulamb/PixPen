using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PixPen.Models;
using PixPen.Services;
using PixPen.Tools;
using PixPen.ViewModels;

namespace PixPen.Controls;

public partial class DrawingCanvas : UserControl
{
    private CanvasTabViewModel? _vm;
    private Matrix _matrix = Matrix.Identity;
    private bool _isPanning;
    private Point _panStart;
    private Matrix _panStartMatrix;
    private bool _isDrawing;
    private StrokePoint? _lastStrokePoint;
    private byte[]? _strokeBeforePixels;
    private int _strokeLayerIndex;

    // ─── WM_POINTER 筆圧 ─────────────────────────────────────────────────────
    // HwndSource.AddHook は特定 HWND にしかフックできないが、
    // EnablePointerSupport 時の WPF は子 HWND で WM_POINTER を処理することがある。
    // ComponentDispatcher.ThreadFilterMessage は UI スレッドの全 HWND を捕捉できる。
    private bool    _threadFilterSubscribed = false;
    private double  _wmPointerPressure = 1.0;
    private bool    _wmPointerIsEraser = false;
    private bool    _wmPointerActive   = false;

    // StylusDown / WinTab が先に処理した場合に MouseDown の二重処理を防ぐフラグ
    private bool _stylusHandledStroke = false;
    // GetPointerPenInfo 高速化のため前回の有効ポインター ID を保持

    public DrawingCanvas()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;

        // マウス入力
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        // PreviewMouseWheel（トンネリング）で先に処理し、親要素への横取りを防ぐ
        PreviewMouseWheel += OnPreviewMouseWheel;

        // PreviewStylusDown/Move/Up（トンネリング）を使用する。
        // バブリングイベント (StylusDown) は子要素に消費されて DrawingCanvas に届かない場合があるが、
        // トンネリングは Window → DrawingCanvas の方向に発火するため確実に捕捉できる。
        PreviewStylusDown += OnStylusDown;
        PreviewStylusMove += OnStylusMove;
        PreviewStylusUp   += OnStylusUp;
        // スタイラス操作時の WPF デフォルト動作を抑制
        Stylus.SetIsPressAndHoldEnabled(this, false);
        Stylus.SetIsFlicksEnabled(this, false);

        // 右クリックでスポイト
        MouseRightButtonDown += OnMouseRightButtonDown;
    }

    // ─── DataContext 切り替え ───────────────────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CanvasTabViewModel oldVm)
        {
            oldVm.CanvasInvalidated -= OnCanvasInvalidated;
            oldVm.PropertyChanged  -= OnVmPropertyChanged;
            if (oldVm.TabletService is WinTabTabletService oldWt)
                oldWt.PacketReceived -= OnWinTabPacket;
        }

        if (e.NewValue is CanvasTabViewModel vm)
        {
            _vm = vm;
            vm.CanvasInvalidated += OnCanvasInvalidated;
            vm.PropertyChanged  += OnVmPropertyChanged;
            if (vm.TabletService is WinTabTabletService wt)
            {
                wt.PacketReceived += OnWinTabPacket;
                // WinTab 出力範囲を取得（比率マッピング用）
                _winTabOrgX         = wt.OutOrgX;
                _winTabOrgY         = wt.OutOrgY;
                _winTabExtX         = wt.OutExtX;
                _winTabExtY         = wt.OutExtY;
                _winTabExtYNegative = wt.OutExtY < 0;
            }
            CompositeImage.Source = vm.CompositeBitmap;
            ApplyCanvasSize(vm.Document.Width, vm.Document.Height);

            // レイアウト完了後に FitToWindow を実行（ActualWidth が確定してから）
            if (vm.Zoom == 1.0 && vm.PanX == 0 && vm.PanY == 0)
                Dispatcher.BeginInvoke(
                    new Action(FitToWindow),
                    System.Windows.Threading.DispatcherPriority.Render);
            else if (IsLoaded)
                RestoreView(vm);
        }
        else
        {
            _vm = null;
            CompositeImage.Source = null;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        ApplyCanvasSize(_vm.Document.Width, _vm.Document.Height);

        if (_vm.Zoom == 1.0 && _vm.PanX == 0 && _vm.PanY == 0)
            FitToWindow();
        else
            RestoreView(_vm);

        // UI スレッドの全メッセージをフィルタリング（子 HWND の WM_POINTER も捕捉）
        if (!_threadFilterSubscribed)
        {
            System.Windows.Interop.ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;
            _threadFilterSubscribed = true;
        }
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_threadFilterSubscribed)
        {
            System.Windows.Interop.ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;
            _threadFilterSubscribed = false;
        }
    }

    /// <summary>
    /// UI スレッドの全 HWND から WM_POINTER を捕捉して筆圧を取得する。
    /// WPF の EnablePointerSupport が子 HWND で WM_POINTER を処理する場合でも
    /// ComponentDispatcher.ThreadFilterMessage は必ず捕捉できる。
    /// </summary>
    private void OnThreadFilterMessage(ref System.Windows.Interop.MSG msg, ref bool handled)
    {
        if (msg.message is PointerNative.WM_POINTERDOWN or PointerNative.WM_POINTERUPDATE)
        {
            uint pid = PointerNative.GetPointerId(msg.wParam);
            if (PointerNative.GetPointerPenInfo(pid, out var info))
            {
                bool hasPressure = info.penMask.HasFlag(PointerNative.PenMask.Pressure);
                _wmPointerPressure = hasPressure
                    ? Math.Clamp(info.pressure / 1024.0, 0.0, 1.0)
                    : 1.0;
                _wmPointerIsEraser = info.penFlags.HasFlag(PointerNative.PenFlags.Inverted) ||
                                     info.penFlags.HasFlag(PointerNative.PenFlags.Eraser);
                _wmPointerActive = true;
            }
        }
        else if (msg.message == PointerNative.WM_POINTERUP)
        {
            _wmPointerActive = false;
        }
    }

    private void ApplyCanvasSize(int width, int height)
    {
        // ContentGrid は ViewportCanvas（Canvas パネル）の子なので
        // Width/Height を設定するだけで確実に固定サイズになる
        ContentGrid.Width  = width;
        ContentGrid.Height = height;
        BackgroundRect.Width  = width;
        BackgroundRect.Height = height;
    }

    private void OnCanvasInvalidated(object? sender, EventArgs e)
    {
        if (_vm == null) return;
        CompositeImage.Source = _vm.CompositeBitmap;
        UpdateGridOverlay();
        UpdateSelectionOverlay();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CanvasTabViewModel.IsGridVisible))
            UpdateGridOverlay();
    }

    // ─── ズーム・パン ────────────────────────────────────────────────────────

    /// <summary>キャンバス全体がビューポートに収まるようズームを調整する</summary>
    public void FitToWindow()
    {
        if (_vm == null) return;
        // ViewportGrid の実サイズを使う（Canvas は 0×0 を報告することがあるため）
        double viewW = ViewportGrid.ActualWidth;
        double viewH = ViewportGrid.ActualHeight;
        if (viewW <= 0 || viewH <= 0) return;

        double docW = _vm.Document.Width;
        double docH = _vm.Document.Height;

        // 少し余白を取って 90% に収める
        double zoom = Math.Min(viewW / docW, viewH / docH) * 0.9;
        zoom = Math.Clamp(zoom, 0.01, 64.0);

        // 中央に配置
        double panX = (viewW - docW * zoom) / 2.0;
        double panY = (viewH - docH * zoom) / 2.0;

        _matrix = new Matrix(zoom, 0, 0, zoom, panX, panY);
        SaveViewToVm();
        ApplyTransform();
    }

    private void RestoreView(CanvasTabViewModel vm)
    {
        _matrix = new Matrix(vm.Zoom, 0, 0, vm.Zoom, vm.PanX, vm.PanY);
        ApplyTransform();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_vm == null) return;
        var pos = e.GetPosition(this);

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Ctrl+スクロール → ズーム（マウス位置を中心に）
            double factor = e.Delta > 0 ? 1.2 : 1.0 / 1.2;
            ZoomAt(pos, factor);
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            // Shift+スクロール → 左右移動
            _matrix.Translate(e.Delta * 0.5, 0);
            SaveViewToVm();
            ApplyTransform();
        }
        else
        {
            // スクロール → 上下移動
            _matrix.Translate(0, e.Delta * 0.5);
            SaveViewToVm();
            ApplyTransform();
        }
        e.Handled = true;
    }

    private void ZoomAt(Point center, double factor)
    {
        _matrix.ScaleAt(factor, factor, center.X, center.Y);
        SaveViewToVm();
        ApplyTransform();
    }

    private void ApplyTransform()
    {
        ViewTransform.Matrix = _matrix;

        // 市松模様タイルサイズをズームの逆数で補正し、画面上で常に 16px に見せる
        // ViewportUnits="Absolute" はキャンバス座標系なので、1/zoom 倍する必要がある
        double zoom = Math.Max(0.001, _matrix.M11);
        double tileSize = 16.0 / zoom;
        CheckerBrush.Viewport = new Rect(0, 0, tileSize, tileSize);

        UpdateGridOverlay();
        UpdateSelectionOverlay();
    }

    private void SaveViewToVm()
    {
        if (_vm == null) return;
        _vm.Zoom = _matrix.M11;
        _vm.PanX = _matrix.OffsetX;
        _vm.PanY = _matrix.OffsetY;
    }

    // ─── マウス入力 ─────────────────────────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null) return;

        // WinTab ペンが離れているのに _stylusHandledStroke が残っている場合はリセット
        // （WinTab の pen-up パケットが届かなかった場合のフォールバック）
        if (_stylusHandledStroke && !_winTabPenWasDown)
            _stylusHandledStroke = false;

        // StylusDown/WinTab が既にストロークを開始していれば無視
        if (_stylusHandledStroke) { e.Handled = true; return; }

        Focus();

        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _isPanning = true;
            _panStart = e.GetPosition(this);
            _panStartMatrix = _matrix;
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var cp = ToCanvasPoint(e.GetPosition(this));

            // スポイト: 左クリック／ドラッグで色取得
            if (_vm.ActiveToolKind == ToolKind.Eyedropper)
            {
                _vm.PickColor((int)cp.X, (int)cp.Y);
                CaptureMouse(); // ドラッグ中も継続取得できるようにキャプチャ
                e.Handled = true;
                return;
            }

            // 塗りつぶしはマウスダウン1回で完結（ストローク不要）
            if (_vm.ActiveToolKind == ToolKind.Fill)
            {
                ExecuteFill(cp);
                e.Handled = true;
                return;
            }

            var (pres, eraser, src) = ResolvePressure();
            if (_vm != null) { _vm.LastPressure = pres; _vm.LastPressureSource = src; }
            BeginStroke(cp, pres, eraser);
            CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_vm == null) return;
        if (_stylusHandledStroke) return;

        if (_isPanning)
        {
            var delta = e.GetPosition(this) - _panStart;
            _matrix = _panStartMatrix;
            _matrix.Translate(delta.X, delta.Y);
            SaveViewToVm();
            ApplyTransform();
            e.Handled = true;
            return;
        }

        // スポイト: ドラッグ中も連続で色取得
        if (_vm.ActiveToolKind == ToolKind.Eyedropper && e.LeftButton == MouseButtonState.Pressed)
        {
            var cp = ToCanvasPoint(e.GetPosition(this));
            _vm.PickColor((int)cp.X, (int)cp.Y);
            e.Handled = true;
            return;
        }

        if (_isDrawing && e.LeftButton == MouseButtonState.Pressed)
        {
            var cp = ToCanvasPoint(e.GetPosition(this));
            var (pres, eraser, _) = ResolvePressure();
            ContinueStroke(cp, pres, eraser);
            e.Handled = true;
        }
    }


    /// <summary>
    /// 利用可能な全ソースから筆圧を解決する。優先順位:
    ///   1. WM_POINTER（Surface Pen 等、Windows Ink 直接）
    ///   2. WinTab API（外付け Wacom タブレット）← 外付け Wacom で唯一動く経路
    ///   3. フォールバック（マウス = 圧力なし）
    /// </summary>
    // ─── WinTab 直接描画 ──────────────────────────────────────────────────────
    // CXO_SYSTEM の WinTab システムコンテキストが有効な場合、Cintiq は
    // WM_LBUTTONDOWN を生成しない（入力が WinTab 経由に切り替わるため）。
    // そのため WinTab パケットから直接描画する必要がある。
    // 座標は GetCursorPos（Win32）で取得後 PointFromScreen で変換することで
    // PointFromScreen(args.X, args.Y) のマルチモニタ DPI 問題を回避する。

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WIN32POINT { public int X, Y; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WIN32POINT point);

    private bool   _winTabPenWasDown   = false;
    private double _winTabOrgX         = 0;
    private double _winTabOrgY         = 0;
    private double _winTabExtX         = 1;
    private double _winTabExtY         = 1;
    private bool   _winTabExtYNegative = false;

    private void OnWinTabPacket(WinTabInputArgs args)
    {
        if (_vm == null) return;

        // SetCursorPos は WinTabTabletService.ProcessPacket で実施済み。
        // GetCursorPos で SetCursorPos 後の実際のカーソル位置を取得し変換する。
        GetCursorPos(out var cursorPos);

        // DrawingCanvas の画面上の位置を取得
        var screenTL = PointToScreen(new Point(0, 0));
        var screenBR = PointToScreen(new Point(ActualWidth, ActualHeight));

        Point elementPt;
        if (cursorPos.X >= screenTL.X && cursorPos.X <= screenBR.X &&
            cursorPos.Y >= screenTL.Y && cursorPos.Y <= screenBR.Y)
        {
            // カーソルが DrawingCanvas の画面内にある → 正常変換
            elementPt = PointFromScreen(new Point(cursorPos.X, cursorPos.Y));
        }
        else
        {
            // カーソルが別モニタにある（アプリを Cintiq に移動する必要がある）
            // 比率マッピング: WinTab 出力範囲に対するカーソル位置を DrawingCanvas に比例マッピング
            double tabletW = Math.Abs(_winTabExtX);
            double tabletH = Math.Abs(_winTabExtY);
            if (tabletW <= 0 || tabletH <= 0)
            {
                if (_vm != null) _vm.LastPressureSource = "WinTab[別モニタ→Cintiq画面にウィンドウを移動]";
                return;
            }
            // WinTab 出力空間内での正規化位置
            double normX = (cursorPos.X - _winTabOrgX) / tabletW;
            double normY = _winTabExtYNegative
                ? 1.0 - (cursorPos.Y - (_winTabOrgY + _winTabExtY)) / tabletH
                : (cursorPos.Y - _winTabOrgY) / tabletH;
            // DrawingCanvas 上の論理ピクセル位置にマッピング
            elementPt = new Point(normX * ActualWidth, normY * ActualHeight);
        }
        var canvasPt  = ToCanvasPoint(elementPt);

        bool wasDown = _winTabPenWasDown;
        _winTabPenWasDown = args.IsDown;

        if (_vm != null) { _vm.LastPressure = args.Pressure; _vm.LastPressureSource = "WinTab"; }

        if (args.IsDown && !wasDown)
        {
            Focus();
            _stylusHandledStroke = true;
            BeginStroke(canvasPt, args.Pressure, args.IsEraser);
        }
        else if (args.IsDown && _isDrawing)
        {
            ContinueStroke(canvasPt, args.Pressure, args.IsEraser);
        }
        else if (!args.IsDown && wasDown && _isDrawing)
        {
            _stylusHandledStroke = false;
            EndStroke();
        }
    }

    /// <summary>
    /// 利用可能なソースから筆圧を解決する。
    /// マウス入力の場合は常に 1.0 固定を返す。
    /// </summary>
    private (double pressure, bool isEraser, string source) ResolvePressure()
    {
        // 1. WM_POINTER（Surface Pen・Windows Ink 対応デバイス）
        if (_wmPointerActive)
            return (_wmPointerPressure, _wmPointerIsEraser, "WMP");

        // 2. WinTab（Wacom Cintiq など外付けタブレットの主要経路）
        // ペンが接触中（_winTabPenWasDown）のときのみ WinTab 筆圧を使用する。
        // ペンが離れてマウスを使う場合は 1.0 固定にしないと、
        // 最後のペン筆圧がマウス描画に引き継がれてしまう。
        if (_vm?.TabletService is WinTabTabletService wt && wt.IsAvailable)
        {
            if (_winTabPenWasDown)
                return (wt.CurrentPressure, wt.CurrentIsEraser, "WinTab");
            // ペン非接触 → マウス入力として扱い 1.0 固定
        }

        // 3. WPF StylusDown（WISP 対応デバイス向け）
        if (App.StylusIsDown)
            return (App.LastStylusPressure, App.LastStylusIsEraser, "WPF");

        // 4. マウス = 筆圧 1.0 固定
        return (1.0, false, "Mouse");
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null) return;
        if (_stylusHandledStroke) return;
        ReleaseMouseCapture();

        if (_isPanning) { _isPanning = false; return; }
        if (_isDrawing) { EndStroke(); e.Handled = true; }
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm == null) return;
        if (_stylusHandledStroke) return;
        var cp = ToCanvasPoint(e.GetPosition(this));
        _vm.PickColor((int)cp.X, (int)cp.Y);
        e.Handled = true;
    }

    // ─── スタイラス（Windows Ink）入力 ─────────────────────────────────────

    private void OnStylusDown(object sender, StylusDownEventArgs e)
    {
        if (_vm == null) return;
        Focus();

        var (cp, pressure, isEraser) = ExtractStylusInfo(e);

        if (_vm.TabletService is WinTabTabletService wt)
        {
            pressure = wt.CurrentPressure;
            isEraser = wt.CurrentIsEraser;
        }

        if (_vm != null) { _vm.LastPressure = pressure; _vm.LastPressureSource = "WPF"; }
        _stylusHandledStroke = true;
        BeginStroke(cp, pressure, isEraser);
        e.Handled = true;
    }

    private void OnStylusMove(object sender, StylusEventArgs e)
    {
        if (_vm == null || !_isDrawing) return;

        var (cp, pressure, isEraser) = ExtractStylusInfo(e);

        if (_vm.TabletService is WinTabTabletService wt)
        {
            pressure = wt.CurrentPressure;
            isEraser = wt.CurrentIsEraser;
        }

        ContinueStroke(cp, pressure, isEraser);
        e.Handled = true;
    }

    private void OnStylusUp(object sender, StylusEventArgs e)
    {
        if (_vm == null) return;
        _stylusHandledStroke = false;
        EndStroke();
        e.Handled = true;
    }

    /// <summary>
    /// StylusEventArgs からキャンバス座標・筆圧・消しゴムフラグを取得する。
    /// App.xaml.cs で EnablePointerSupport = true にしているため、
    /// WM_POINTER 経由の正確な筆圧が PressureFactor に格納される。
    /// </summary>
    private (Point canvasPoint, double pressure, bool isEraser) ExtractStylusInfo(StylusEventArgs e)
    {
        var cp = ToCanvasPoint(e.GetPosition(this));

        double pressure = 1.0;
        try
        {
            // イベント由来の点群（EnablePointerSupport 時は WM_POINTER の圧力が入っている）
            var pts = e.GetStylusPoints(this);
            if (pts.Count > 0)
                pressure = Math.Clamp((double)pts[pts.Count - 1].PressureFactor, 0.0, 1.0);

            // フォールバック：イベント由来が空なら StylusDevice から取得
            if (pts.Count == 0)
            {
                var devPts = e.StylusDevice.GetStylusPoints(null);
                if (devPts.Count > 0)
                    pressure = Math.Clamp((double)devPts[devPts.Count - 1].PressureFactor, 0.0, 1.0);
            }
        }
        catch { }

        bool isEraser = e.StylusDevice.Inverted;
        return (cp, pressure, isEraser);
    }

    // ─── ストローク処理 ──────────────────────────────────────────────────────

    private void BeginStroke(Point canvasPoint, double pressure, bool isEraser)
    {
        if (_vm == null || _vm.ActiveLayer == null || _vm.ActiveLayer.Bitmap == null) return;

        // ストローク開始時に選択中のペン定義を PenTool と同期する。
        // 同じオブジェクト参照を共有するため、UI での Size 等の変更が次ストロークから反映される。
        if (!isEraser && _vm.PenPanel.SelectedPen != null)
            _vm.PenTool.Definition = _vm.PenPanel.SelectedPen;

        _isDrawing = true;
        _strokeLayerIndex = _vm.ActiveLayerIndex;

        var layer = _vm.ActiveLayer;
        int len = layer.Width * layer.Height * 4;
        _strokeBeforePixels = new byte[len];
        layer.Bitmap.CopyPixels(new Int32Rect(0, 0, layer.Width, layer.Height),
            _strokeBeforePixels, layer.Width * 4, 0);

        var sp = new StrokePoint(canvasPoint.X, canvasPoint.Y, pressure, isEraser);
        var tool = GetEffectiveTool(isEraser);
        var dirty = tool.OnDown(sp, layer, _vm.Document.Palette);
        _lastStrokePoint = sp;

        if (dirty.Width > 0) _vm.RecompositeRegion(dirty);
    }

    private void ContinueStroke(Point canvasPoint, double pressure, bool isEraser)
    {
        if (_vm == null || !_isDrawing || _vm.ActiveLayer == null) return;
        var sp = new StrokePoint(canvasPoint.X, canvasPoint.Y, pressure, isEraser);
        var tool = GetEffectiveTool(isEraser);
        var dirty = tool.OnMove(sp, _vm.ActiveLayer, _vm.Document.Palette);
        _lastStrokePoint = sp;
        if (dirty.Width > 0) _vm.RecompositeRegion(dirty);
    }

    private void EndStroke()
    {
        if (_vm == null || !_isDrawing) return;
        _isDrawing = false;
        _stylusHandledStroke = false;  // 次のストロークのためにリセット

        if (_vm.ActiveLayer?.Bitmap != null && _strokeBeforePixels != null)
        {
            var layer = _vm.ActiveLayer;
            var sp = _lastStrokePoint ?? new StrokePoint(0, 0, 0, false);
            GetEffectiveTool(sp.IsEraser).OnUp(sp, layer, _vm.Document.Palette);

            var afterPixels = new byte[_strokeBeforePixels.Length];
            layer.Bitmap.CopyPixels(new Int32Rect(0, 0, layer.Width, layer.Height),
                afterPixels, layer.Width * 4, 0);

            _vm.PushStrokeUndo(_strokeLayerIndex,
                new Int32Rect(0, 0, layer.Width, layer.Height),
                _strokeBeforePixels, afterPixels);
        }

        _strokeBeforePixels = null;
        if (_vm != null) _vm.Document.IsModified = true;
    }

    /// <summary>塗りつぶしをUndoに対応した形で1回実行する</summary>
    private void ExecuteFill(Point canvasPoint)
    {
        if (_vm == null || _vm.ActiveLayer == null || _vm.ActiveLayer.Bitmap == null) return;

        var layer = _vm.ActiveLayer;

        // Undo のためにビットマップを事前コピー
        var before = new byte[layer.Width * layer.Height * 4];
        layer.Bitmap.CopyPixels(new Int32Rect(0, 0, layer.Width, layer.Height),
            before, layer.Width * 4, 0);

        var sp = new StrokePoint(canvasPoint.X, canvasPoint.Y, 1.0, false);
        var dirty = _vm.FillTool.OnDown(sp, layer, _vm.Document.Palette);

        if (dirty.Width <= 0) return;

        var after = new byte[before.Length];
        layer.Bitmap.CopyPixels(new Int32Rect(0, 0, layer.Width, layer.Height),
            after, layer.Width * 4, 0);

        _vm.PushStrokeUndo(_vm.ActiveLayerIndex,
            new Int32Rect(0, 0, layer.Width, layer.Height), before, after);

        _vm.Document.IsModified = true;
        _vm.RefreshTitle();
        _vm.RecompositeRegion(dirty);
    }

    private ITool GetEffectiveTool(bool isEraser)
    {
        if (_vm == null) return new PenTool();
        if (isEraser) return _vm.EraserTool;
        return _vm.ActiveToolKind switch
        {
            ToolKind.Pen        => _vm.PenTool,
            ToolKind.Eraser     => _vm.EraserTool,
            ToolKind.Eyedropper => _vm.EyedropperTool,
            ToolKind.Selection  => _vm.SelectionTool,
            ToolKind.Fill       => _vm.FillTool,
            _                   => _vm.PenTool
        };
    }

    // ─── 座標変換 ───────────────────────────────────────────────────────────

    private Point ToCanvasPoint(Point screenPoint)
    {
        var inv = _matrix;
        inv.Invert();
        return inv.Transform(screenPoint);
    }

    // ─── グリッドオーバーレイ ────────────────────────────────────────────────

    private void UpdateGridOverlay()
    {
        GridOverlay.Children.Clear();
        if (_vm == null || !_vm.IsGridVisible) return;
        var grid = _vm.Document.Grid;
        DrawGrid(grid.Small);
        DrawGrid(grid.Medium);
        DrawGrid(grid.Large);
    }

    private void DrawGrid(SingleGridSettings gs)
    {
        if (!gs.Visible) return;
        if (gs.SpacingX <= 0 || gs.SpacingY <= 0) return;

        int docW = _vm!.Document.Width, docH = _vm.Document.Height;

        // ライン数が多すぎる場合はスキップ（パフォーマンス保護）
        int totalLines = (int)(docW / gs.SpacingX) + (int)(docH / gs.SpacingY) + 2;
        if (totalLines > 2000) return;

        var color = ColorHelper.FromArgbHex(gs.Color);
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        // ズームに合わせて画面上で常に約 1px に見える太さ
        double thickness = Math.Max(0.1, 1.0 / _matrix.M11);

        DoubleCollection? dashArray = gs.LineType == GridLineType.Dashed
            ? new DoubleCollection(new[] { 4.0, 4.0 }) : null;

        // オフセットを正規化（負の値にも対応）
        double startX = ((gs.OffsetX % gs.SpacingX) + gs.SpacingX) % gs.SpacingX;
        double startY = ((gs.OffsetY % gs.SpacingY) + gs.SpacingY) % gs.SpacingY;

        // 縦線
        for (double x = startX; x <= docW; x += gs.SpacingX)
        {
            GridOverlay.Children.Add(new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = docH,
                Stroke = brush, StrokeThickness = thickness,
                StrokeDashArray = dashArray
            });
        }

        // 横線
        for (double y = startY; y <= docH; y += gs.SpacingY)
        {
            GridOverlay.Children.Add(new Line
            {
                X1 = 0, Y1 = y, X2 = docW, Y2 = y,
                Stroke = brush, StrokeThickness = thickness,
                StrokeDashArray = dashArray
            });
        }
    }

    // ─── 選択範囲オーバーレイ ────────────────────────────────────────────────

    private void UpdateSelectionOverlay()
    {
        if (_vm?.Selection is not { HasSelection: true } sel)
        {
            SelectionRect.Visibility = Visibility.Collapsed;
            return;
        }
        Canvas.SetLeft(SelectionRect, sel.X);
        Canvas.SetTop(SelectionRect, sel.Y);
        SelectionRect.Width = sel.Width;
        SelectionRect.Height = sel.Height;
        SelectionRect.StrokeThickness = 1.0 / _matrix.M11;
        SelectionRect.Visibility = Visibility.Visible;
    }
}
