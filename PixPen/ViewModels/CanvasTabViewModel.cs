using System.Windows;
using System.Windows.Media.Imaging;
using PixPen.Models;
using PixPen.Services;
using PixPen.Tools;

namespace PixPen.ViewModels;

public class CanvasTabViewModel : ViewModelBase
{
    // ─── 公開プロパティ ─────────────────────────────────────────────────────

    public Document Document { get; }
    public UndoRedoService UndoRedo { get; }
    private ITabletService? _tabletService;
    public ITabletService? TabletService
    {
        get => _tabletService;
        set => SetField(ref _tabletService, value);
    }
    public int MaxLayers { get; set; } = 100;

    public WriteableBitmap CompositeBitmap { get; private set; }

    // フローティングオーバーレイ（選択移動中にレイヤーを変更せず一時描画する）
    private WriteableBitmap? _floatingOverlay;
    private int _floatOvlX, _floatOvlY;

    internal void SetFloatingOverlay(WriteableBitmap overlay, int x, int y)
    {
        _floatingOverlay = overlay;
        _floatOvlX = x; _floatOvlY = y;
    }
    internal void UpdateFloatingOverlayPos(int x, int y) { _floatOvlX = x; _floatOvlY = y; }
    internal void ClearFloatingOverlay() { _floatingOverlay = null; }

    private int _activeLayerIndex;
    public int ActiveLayerIndex
    {
        get => _activeLayerIndex;
        set
        {
            value = Math.Clamp(value, 0, Math.Max(0, Document.Layers.Count - 1));
            SetField(ref _activeLayerIndex, value);
        }
    }

    public Layer? ActiveLayer =>
        _activeLayerIndex >= 0 && _activeLayerIndex < Document.Layers.Count
            ? Document.Layers[_activeLayerIndex] : null;

    private ToolKind _activeToolKind = ToolKind.Pen;
    public ToolKind ActiveToolKind
    {
        get => _activeToolKind;
        set => SetField(ref _activeToolKind, value);
    }

    private bool _isGridVisible;
    public bool IsGridVisible
    {
        get => _isGridVisible;
        set { SetField(ref _isGridVisible, value); InvalidateCanvas(); }
    }

    public SelectionMask Selection { get; } = new();

    // 直近の筆圧（ステータスバー表示用）
    private double _lastPressure;
    public double LastPressure
    {
        get => _lastPressure;
        set => SetField(ref _lastPressure, value);
    }

    private string _lastPressureSource = "-";
    public string LastPressureSource
    {
        get => _lastPressureSource;
        set => SetField(ref _lastPressureSource, value);
    }

    // ズーム・パン（DrawingCanvas から読み書き、INPC でステータスバーを更新）
    private double _zoom = 1.0;
    public double Zoom
    {
        get => _zoom;
        set => SetField(ref _zoom, value);
    }
    private double _panX, _panY;
    public double PanX { get => _panX; set => SetField(ref _panX, value); }
    public double PanY { get => _panY; set => SetField(ref _panY, value); }

    // ─── ツール ────────────────────────────────────────────────────────────

    public PenTool PenTool { get; } = new();
    public EraserTool EraserTool { get; } = new();
    public FillTool FillTool { get; } = new();
    public EyedropperTool EyedropperTool { get; }
    public SelectionTool SelectionTool { get; }

    // ─── パネル VM ─────────────────────────────────────────────────────────

    public LayerPanelViewModel LayerPanel { get; }
    public ColorPalettePanelViewModel ColorPanel { get; }
    public PenPalettePanelViewModel PenPanel { get; }

    // DrawingCanvas へ再描画要求を通知
    public event EventHandler? CanvasInvalidated;
    // DrawingCanvas へキャンバスサイズ変更を通知
    public event EventHandler? CanvasSizeChanged;

    // ─── タイトル ──────────────────────────────────────────────────────────

    public string Title => Document.IsModified
        ? $"* {Document.Title}" : Document.Title;

    // ─── コンストラクタ ─────────────────────────────────────────────────────

    public CanvasTabViewModel(Document document, UndoRedoService undoRedo, int maxLayers)
    {
        Document = document;
        UndoRedo = undoRedo;
        MaxLayers = maxLayers;

        CompositeBitmap = FileService.CreateTransparentBitmap(document.Width, document.Height, document.Dpi);

        EyedropperTool = new EyedropperTool
        {
            CompositeBitmap = CompositeBitmap,
            // ColorPanel は後で初期化されるが、ラムダはクロージャとして遅延評価される
            OnColorPicked = _ => ColorPanel?.RefreshForeground()
        };

        SelectionTool = new SelectionTool
        {
            Mask = Selection,
            OnSelectionChanged = InvalidateCanvas
        };

        LayerPanel = new LayerPanelViewModel(this);
        ColorPanel = new ColorPalettePanelViewModel(document.Palette);
        PenPanel = new PenPalettePanelViewModel(document.Pens);

        // ペンツールに色パレットの筆圧カーブを同期
        UndoRedo.StateChanged += (_, _) => { OnPropertyChanged(nameof(Title)); };

        // 初期合成
        RecompositeAll();
    }

    // ─── 合成 ──────────────────────────────────────────────────────────────

    public void RecompositeAll()
        => RecompositeRegion(new Int32Rect(0, 0, Document.Width, Document.Height));

    public void RecompositeRegion(Int32Rect dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0) return;
        int w = Document.Width, h = Document.Height;
        dirtyRect = ClampRect(dirtyRect, w, h);
        if (dirtyRect.Width <= 0) return;

        CompositeBitmap.Lock();
        unsafe
        {
            byte* dstPtr = (byte*)CompositeBitmap.BackBuffer;
            int dstStride = CompositeBitmap.BackBufferStride;

            // 対象領域をクリア
            for (int y = dirtyRect.Y; y < dirtyRect.Y + dirtyRect.Height; y++)
            {
                var row = new Span<byte>(dstPtr + y * dstStride + dirtyRect.X * 4, dirtyRect.Width * 4);
                row.Clear();
            }

            // レイヤーを下から合成
            foreach (var layer in Enumerable.Reverse(Document.Layers))
            {
                if (!layer.IsVisible || layer.Bitmap == null) continue;
                layer.Bitmap.Lock();
                byte* srcPtr = (byte*)layer.Bitmap.BackBuffer;
                int srcStride = layer.Bitmap.BackBufferStride;
                byte opacityByte = (byte)(layer.Opacity * 255);

                for (int y = dirtyRect.Y; y < dirtyRect.Y + dirtyRect.Height && y < layer.Height; y++)
                for (int x = dirtyRect.X; x < dirtyRect.X + dirtyRect.Width && x < layer.Width; x++)
                {
                    byte* src = srcPtr + y * srcStride + x * 4;
                    byte* dst = dstPtr + y * dstStride + x * 4;
                    FileService.AlphaComposite(src, dst, opacityByte);
                }
                layer.Bitmap.Unlock();
            }

            // フローティングオーバーレイを最前面に合成（選択移動中）
            if (_floatingOverlay != null)
            {
                int ovlX = _floatOvlX, ovlY = _floatOvlY;
                int ovlW = _floatingOverlay.PixelWidth, ovlH = _floatingOverlay.PixelHeight;
                int cx  = Math.Max(dirtyRect.X, ovlX);
                int cy  = Math.Max(dirtyRect.Y, ovlY);
                int cx2 = Math.Min(dirtyRect.X + dirtyRect.Width,  ovlX + ovlW);
                int cy2 = Math.Min(dirtyRect.Y + dirtyRect.Height, ovlY + ovlH);
                if (cx < cx2 && cy < cy2)
                {
                    _floatingOverlay.Lock();
                    byte* ovlPtr = (byte*)_floatingOverlay.BackBuffer;
                    int ovlStride = _floatingOverlay.BackBufferStride;
                    for (int y = cy; y < cy2; y++)
                    for (int x = cx; x < cx2; x++)
                    {
                        byte* src = ovlPtr + (y - ovlY) * ovlStride + (x - ovlX) * 4;
                        byte* dst = dstPtr + y * dstStride + x * 4;
                        FileService.AlphaComposite(src, dst, 255);
                    }
                    _floatingOverlay.Unlock();
                }
            }
        }
        CompositeBitmap.AddDirtyRect(dirtyRect);
        CompositeBitmap.Unlock();

        CanvasInvalidated?.Invoke(this, EventArgs.Empty);
    }

    // ─── 選択操作（削除・コピー・カット・ペースト） ────────────────────────────

    /// <summary>クリップボードまたはキャンバス内部からペーストするよう DrawingCanvas に通知するイベント</summary>
    public event Action<byte[], int, int>? PasteRequested; // pixels, width, height

    /// <summary>選択領域をアクティブレイヤーから削除する（Undo対応）</summary>
    public void DeleteSelection()
    {
        if (!Selection.HasSelection || ActiveLayer?.Bitmap == null) return;
        int bW = Document.Width, bH = Document.Height;
        int sx = Math.Max(0, Selection.X), sy = Math.Max(0, Selection.Y);
        int sw = Math.Min(Selection.Width, bW - sx);
        int sh = Math.Min(Selection.Height, bH - sy);
        if (sw <= 0 || sh <= 0) return;

        var before = new byte[bW * bH * 4];
        ActiveLayer.Bitmap.CopyPixels(new Int32Rect(0, 0, bW, bH), before, bW * 4, 0);

        ActiveLayer.Bitmap.WritePixels(
            new Int32Rect(sx, sy, sw, sh), new byte[sw * sh * 4], sw * 4, 0);

        var after = new byte[bW * bH * 4];
        ActiveLayer.Bitmap.CopyPixels(new Int32Rect(0, 0, bW, bH), after, bW * 4, 0);

        PushStrokeUndo(ActiveLayerIndex, new Int32Rect(0, 0, bW, bH), before, after);
        Document.IsModified = true;
        Selection.Clear();
        RecompositeRegion(new Int32Rect(sx, sy, sw, sh));
        InvalidateCanvas();
        RefreshTitle();
    }

    /// <summary>
    /// 選択領域をクリップボードにコピーする（合成済みビットマップから取得）。
    /// アルファを保持するため PNG 形式も同時に保存する。
    /// SetImage() だけでは DIB(Bgr32) になり透明ピクセルが黒になるため。
    /// </summary>
    public void CopySelectionToClipboard()
    {
        if (!Selection.HasSelection) return;
        int bW = Document.Width, bH = Document.Height;
        int sx = Math.Max(0, Selection.X), sy = Math.Max(0, Selection.Y);
        int sw = Math.Min(Selection.Width, bW - sx);
        int sh = Math.Min(Selection.Height, bH - sy);
        if (sw <= 0 || sh <= 0) return;

        var pixels = new byte[sw * sh * 4];
        CompositeBitmap.CopyPixels(new Int32Rect(sx, sy, sw, sh), pixels, sw * 4, 0);

        var bmpSrc = System.Windows.Media.Imaging.BitmapSource.Create(
            sw, sh, 96, 96,
            System.Windows.Media.PixelFormats.Bgra32, null, pixels, sw * 4);

        // PNG にエンコードしてアルファ付きでクリップボードに格納
        var dataObj = new System.Windows.DataObject();
        dataObj.SetImage(bmpSrc); // 標準形式（外部アプリ向け・アルファなし）

        using var ms = new System.IO.MemoryStream();
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmpSrc));
        encoder.Save(ms);
        dataObj.SetData("PNG", ms.ToArray()); // アルファ付き PNG（内部・外部共通）

        System.Windows.Clipboard.SetDataObject(dataObj, copy: true);
    }

    /// <summary>選択領域をクリップボードにコピーしてレイヤーから削除する</summary>
    public void CutSelection()
    {
        CopySelectionToClipboard();
        DeleteSelection();
    }

    /// <summary>
    /// クリップボードの画像をフローティング選択として貼り付ける。
    /// PNG 形式を優先して読み込むことでアルファチャンネルを正しく保持する。
    /// </summary>
    public void PasteFromClipboard()
    {
        try
        {
            byte[]? pixels = null;
            int w = 0, h = 0;

            // ─ 優先: PNG（アルファ保持） ─
            if (System.Windows.Clipboard.ContainsData("PNG"))
            {
                var pngBytes = System.Windows.Clipboard.GetData("PNG") as byte[];
                if (pngBytes != null && pngBytes.Length > 0)
                {
                    using var ms = new System.IO.MemoryStream(pngBytes);
                    var decoder = new System.Windows.Media.Imaging.PngBitmapDecoder(
                        ms,
                        System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                        System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                    var frame = decoder.Frames[0];
                    var converted = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                        frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
                    w = converted.PixelWidth; h = converted.PixelHeight;
                    pixels = new byte[w * h * 4];
                    converted.CopyPixels(pixels, w * 4, 0);
                }
            }

            // ─ フォールバック: 標準画像形式（アルファなし、外部アプリ貼り付け） ─
            if (pixels == null && System.Windows.Clipboard.ContainsImage())
            {
                var imgSrc = System.Windows.Clipboard.GetImage();
                if (imgSrc != null)
                {
                    var converted = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                        imgSrc, System.Windows.Media.PixelFormats.Bgra32, null, 0);
                    w = converted.PixelWidth; h = converted.PixelHeight;
                    pixels = new byte[w * h * 4];
                    converted.CopyPixels(pixels, w * 4, 0);
                    // DIB 由来の画像はアルファが 0 になる場合があるので 255 に補正
                    for (int i = 3; i < pixels.Length; i += 4)
                        if (pixels[i] == 0) pixels[i] = 255;
                }
            }

            if (pixels != null && w > 0 && h > 0)
                PasteRequested?.Invoke(pixels, w, h);
        }
        catch (Exception ex) { Services.Logger.Error("PasteFromClipboard failed", ex); }
    }

    // ─── Undo/Redo ─────────────────────────────────────────────────────────

    public void PushStrokeUndo(int layerIndex, Int32Rect region, byte[] before, byte[] after)
    {
        UndoRedo.Push(new StrokeUndoAction
        {
            LayerIndex = layerIndex,
            Region = region,
            BeforePixels = before,
            AfterPixels = after
        });
        Document.IsModified = true;
        OnPropertyChanged(nameof(Title));
    }

    public void Undo()
    {
        UndoRedo.Undo(Document, OnLayerModified);
        LayerPanel.SyncFromDocument();
        Document.IsModified = true;
        OnPropertyChanged(nameof(Title));
    }

    public void Redo()
    {
        UndoRedo.Redo(Document, OnLayerModified);
        LayerPanel.SyncFromDocument();
        Document.IsModified = true;
        OnPropertyChanged(nameof(Title));
    }

    private void OnLayerModified(int layerIndex)
        => RecompositeAll();

    // ─── スポイト ──────────────────────────────────────────────────────────

    public void PickColor(int x, int y)
    {
        var (r, g, b, a) = BitmapHelper.GetPixel(CompositeBitmap, x, y);
        var hex = $"#{a:X2}{r:X2}{g:X2}{b:X2}";
        Document.Palette.ForegroundColor = hex;
        ColorPanel.Foreground = Services.ColorHelper.FromArgbHex(hex);
    }

    // ─── ヘルパー ──────────────────────────────────────────────────────────

    public void InvalidateCanvas() => CanvasInvalidated?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// キャンバスをリサイズする（左上基準）。
    /// 縮小時は右下をカット、拡大時は右下を透明で拡張。
    /// リサイズ後は Undo 履歴をクリアする。
    /// </summary>
    public void ResizeCanvas(int newWidth, int newHeight)
    {
        newWidth  = Math.Max(1, Math.Min(newWidth,  32767));
        newHeight = Math.Max(1, Math.Min(newHeight, 32767));
        if (newWidth == Document.Width && newHeight == Document.Height) return;

        Services.Logger.Info($"ResizeCanvas: {Document.Width}x{Document.Height} → {newWidth}x{newHeight}");

        // 全レイヤーをリサイズ
        foreach (var layer in Document.Layers)
        {
            if (layer.Bitmap == null) continue;
            layer.Bitmap = Services.FileService.ResizeLayerBitmap(
                layer.Bitmap, newWidth, newHeight, Document.Dpi);
        }

        // ドキュメントサイズを更新
        Document.Width  = newWidth;
        Document.Height = newHeight;
        Document.IsModified = true;

        // CompositeBitmap を新サイズで再生成（スポイトツールも更新）
        CompositeBitmap = Services.FileService.CreateTransparentBitmap(newWidth, newHeight, Document.Dpi);
        EyedropperTool.CompositeBitmap = CompositeBitmap;

        // リサイズ後は Undo 履歴が無効になるのでクリア
        UndoRedo.Clear();

        // キャンバスサイズ変更を DrawingCanvas に通知
        CanvasSizeChanged?.Invoke(this, EventArgs.Empty);

        // 再合成
        RecompositeAll();
        RefreshTitle();
    }

    public void RefreshTitle() => OnPropertyChanged(nameof(Title));

    private static Int32Rect ClampRect(Int32Rect r, int maxW, int maxH)
    {
        int x = Math.Max(0, r.X), y = Math.Max(0, r.Y);
        int x2 = Math.Min(maxW, r.X + r.Width);
        int y2 = Math.Min(maxH, r.Y + r.Height);
        return new Int32Rect(x, y, Math.Max(0, x2 - x), Math.Max(0, y2 - y));
    }
}
