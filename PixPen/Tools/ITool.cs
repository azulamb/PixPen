using System.Windows;
using PixPen.Models;

namespace PixPen.Tools;

public record StrokePoint(double X, double Y, double Pressure, bool IsEraser);

public interface ITool
{
    ToolKind Kind { get; }

    // 戻り値は変更された領域（Undo用・再合成用）。変更なしは empty rect
    Int32Rect OnDown(StrokePoint point, Layer activeLayer, ColorPalette palette);
    Int32Rect OnMove(StrokePoint point, Layer activeLayer, ColorPalette palette);
    Int32Rect OnUp(StrokePoint point, Layer activeLayer, ColorPalette palette);
}
