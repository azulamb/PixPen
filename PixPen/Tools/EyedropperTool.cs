using System.Windows;
using System.Windows.Media.Imaging;
using PixPen.Models;

namespace PixPen.Tools;

public class EyedropperTool : ITool
{
    public ToolKind Kind => ToolKind.Eyedropper;

    // 色を取得した後に呼ばれるコールバック
    public Action<string>? OnColorPicked { get; set; }

    // 合成済みビットマップを指定する（全レイヤー合成色を取得するため）
    public WriteableBitmap? CompositeBitmap { get; set; }

    public Int32Rect OnDown(StrokePoint point, Layer layer, ColorPalette palette)
    {
        PickColor((int)point.X, (int)point.Y, palette);
        return default;
    }

    public Int32Rect OnMove(StrokePoint point, Layer layer, ColorPalette palette) => default;

    public Int32Rect OnUp(StrokePoint point, Layer layer, ColorPalette palette) => default;

    private void PickColor(int x, int y, ColorPalette palette)
    {
        if (CompositeBitmap == null) return;
        var (r, g, b, a) = BitmapHelper.GetPixel(CompositeBitmap, x, y);
        var hex = $"#{a:X2}{r:X2}{g:X2}{b:X2}";
        palette.ForegroundColor = hex;
        OnColorPicked?.Invoke(hex);
    }
}
