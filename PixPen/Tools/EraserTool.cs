using System.Windows;
using PixPen.Models;

namespace PixPen.Tools;

public class EraserTool : ITool
{
    public ToolKind Kind => ToolKind.Eraser;
    public double Size { get; set; } = 20.0;
    public PenShape Shape { get; set; } = PenShape.Round;

    private double _lastX, _lastY;
    private bool _hasLastPoint;

    public Int32Rect OnDown(StrokePoint point, Layer layer, ColorPalette palette)
    {
        _hasLastPoint = false;
        return Stamp(point, layer);
    }

    public Int32Rect OnMove(StrokePoint point, Layer layer, ColorPalette palette)
    {
        if (!_hasLastPoint) return Stamp(point, layer);
        int radius = Math.Max(1, (int)(Size / 2));
        var dirty = BitmapHelper.InterpolateStamp(
            layer.Bitmap!, _lastX, _lastY, point.X, point.Y,
            radius, 0, 0, 0, 255, Shape == PenShape.Round, erase: true);
        _lastX = point.X; _lastY = point.Y;
        return dirty;
    }

    public Int32Rect OnUp(StrokePoint point, Layer layer, ColorPalette palette)
    {
        _hasLastPoint = false;
        return default;
    }

    private Int32Rect Stamp(StrokePoint point, Layer layer)
    {
        if (layer.Bitmap == null) return default;
        int radius = Math.Max(1, (int)(Size / 2));
        var dirty = Shape == PenShape.Round
            ? BitmapHelper.StampRound(layer.Bitmap, (int)point.X, (int)point.Y, radius, 0, 0, 0, 255, erase: true)
            : BitmapHelper.StampSquare(layer.Bitmap, (int)point.X, (int)point.Y, radius, 0, 0, 0, 255, erase: true);
        _lastX = point.X; _lastY = point.Y; _hasLastPoint = true;
        return dirty;
    }
}
