using System.Windows;
using System.Windows.Media.Imaging;
using PixPen.Models;

namespace PixPen.Tools;

public class SelectionTool : ITool
{
    public ToolKind Kind => ToolKind.Selection;
    public SelectionMask? Mask { get; set; }
    public Action? OnSelectionChanged { get; set; }

    private double _startX, _startY;

    public Int32Rect OnDown(StrokePoint point, Layer layer, ColorPalette palette)
    {
        _startX = point.X;
        _startY = point.Y;
        Mask?.Clear();
        OnSelectionChanged?.Invoke();
        return default;
    }

    public Int32Rect OnMove(StrokePoint point, Layer layer, ColorPalette palette)
    {
        if (Mask == null) return default;
        int x = (int)Math.Min(_startX, point.X);
        int y = (int)Math.Min(_startY, point.Y);
        int w = (int)Math.Abs(point.X - _startX);
        int h = (int)Math.Abs(point.Y - _startY);
        Mask.SetRect(x, y, w, h);
        OnSelectionChanged?.Invoke();
        return default;
    }

    public Int32Rect OnUp(StrokePoint point, Layer layer, ColorPalette palette)
    {
        OnMove(point, layer, palette);
        return default;
    }

    // 選択範囲をクリップボードにコピー
    public void CopySelection(Layer layer)
    {
        if (Mask == null || !Mask.HasSelection || layer.Bitmap == null) return;
        var pixels = new byte[Mask.Width * Mask.Height * 4];
        layer.Bitmap.CopyPixels(Mask.Rect, pixels, Mask.Width * 4, 0);
        // クリップボードへの書き込みは CanvasTabViewModel が担当
    }

    // 選択範囲を切り取り（透明に）
    public void CutSelection(Layer layer)
    {
        if (Mask == null || !Mask.HasSelection || layer.Bitmap == null) return;
        layer.Bitmap.Lock();
        unsafe
        {
            byte* ptr = (byte*)layer.Bitmap.BackBuffer;
            int stride = layer.Bitmap.BackBufferStride;
            for (int y = Mask.Y; y < Mask.Y + Mask.Height; y++)
            for (int x = Mask.X; x < Mask.X + Mask.Width; x++)
            {
                byte* p = ptr + y * stride + x * 4;
                p[0] = p[1] = p[2] = p[3] = 0;
            }
        }
        layer.Bitmap.AddDirtyRect(Mask.Rect);
        layer.Bitmap.Unlock();
    }
}
