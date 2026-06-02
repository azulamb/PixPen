using System.Windows;
using System.Windows.Media.Imaging;
using PixPen.Models;
using PixPen.Services;

namespace PixPen.Tools;

/// <summary>
/// 塗りつぶしツール（スキャンライン・フラッドフィル）。
/// クリック位置の色と同色で隣接するピクセルをすべて前景色で塗りつぶす。
/// アンチエイリアスなし・整数ピクセル単位。
/// </summary>
public class FillTool : ITool
{
    public ToolKind Kind => ToolKind.Fill;

    public Int32Rect OnDown(StrokePoint point, Layer layer, ColorPalette palette)
    {
        if (layer.Bitmap == null) return default;

        int x = (int)point.X;
        int y = (int)point.Y;
        int w = layer.Bitmap.PixelWidth;
        int h = layer.Bitmap.PixelHeight;

        if (x < 0 || x >= w || y < 0 || y >= h) return default;

        // 前景色を取得
        var fg = ColorHelper.FromArgbHex(palette.ForegroundColor);
        byte fillB = fg.B, fillG = fg.G, fillR = fg.R, fillA = fg.A;

        layer.Bitmap.Lock();
        try
        {
            unsafe
            {
                byte* ptr = (byte*)layer.Bitmap.BackBuffer;
                int stride = layer.Bitmap.BackBufferStride;

                // クリック位置のターゲット色を記録
                byte* target = ptr + y * stride + x * 4;
                byte tB = target[0], tG = target[1], tR = target[2], tA = target[3];

                // ターゲット色と塗りつぶし色が同じなら何もしない
                if (tB == fillB && tG == fillG && tR == fillR && tA == fillA)
                    return default;

                int minX = x, maxX = x, minY = y, maxY = y;

                // スキャンライン・フラッドフィル
                var stack = new Stack<(int x, int y)>();
                stack.Push((x, y));

                while (stack.Count > 0)
                {
                    var (cx, cy) = stack.Pop();
                    if (cx < 0 || cx >= w || cy < 0 || cy >= h) continue;

                    byte* p = ptr + cy * stride + cx * 4;
                    if (p[0] != tB || p[1] != tG || p[2] != tR || p[3] != tA) continue;

                    // このスキャンラインを左右に延ばして塗る
                    int left = cx, right = cx;
                    while (left > 0)
                    {
                        byte* lp = ptr + cy * stride + (left - 1) * 4;
                        if (lp[0] == tB && lp[1] == tG && lp[2] == tR && lp[3] == tA) left--;
                        else break;
                    }
                    while (right < w - 1)
                    {
                        byte* rp = ptr + cy * stride + (right + 1) * 4;
                        if (rp[0] == tB && rp[1] == tG && rp[2] == tR && rp[3] == tA) right++;
                        else break;
                    }

                    // 行を塗りつぶす
                    for (int px = left; px <= right; px++)
                    {
                        byte* pp = ptr + cy * stride + px * 4;
                        pp[0] = fillB; pp[1] = fillG; pp[2] = fillR; pp[3] = fillA;
                    }

                    // 範囲を更新
                    if (left  < minX) minX = left;
                    if (right > maxX) maxX = right;
                    if (cy < minY) minY = cy;
                    if (cy > maxY) maxY = cy;

                    // 上下の行をスタックに積む
                    if (cy > 0)
                        for (int px = left; px <= right; px++)
                        {
                            byte* up = ptr + (cy - 1) * stride + px * 4;
                            if (up[0] == tB && up[1] == tG && up[2] == tR && up[3] == tA)
                            { stack.Push((px, cy - 1)); px++; } // 隣接を連続してプッシュしない最適化
                        }
                    if (cy < h - 1)
                        for (int px = left; px <= right; px++)
                        {
                            byte* dn = ptr + (cy + 1) * stride + px * 4;
                            if (dn[0] == tB && dn[1] == tG && dn[2] == tR && dn[3] == tA)
                            { stack.Push((px, cy + 1)); px++; }
                        }
                }

                var dirty = new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                layer.Bitmap.AddDirtyRect(dirty);
                return dirty;
            }
        }
        finally
        {
            layer.Bitmap.Unlock();
        }
    }

    public Int32Rect OnMove(StrokePoint point, Layer layer, ColorPalette palette) => default;
    public Int32Rect OnUp(StrokePoint point, Layer layer, ColorPalette palette) => default;
}
