using System.Windows;
using System.Windows.Media.Imaging;

namespace PixPen.Tools;

/// <summary>
/// WriteableBitmap へのピクセル書き込みユーティリティ（アンチエイリアス無効）。
/// </summary>
internal static class BitmapHelper
{
    // ピクセルの Porter-Duff over 合成（Bgra32 前提）
    internal static unsafe void BlendPixel(byte* dst, byte srcR, byte srcG, byte srcB, byte srcA)
    {
        if (srcA == 0) return;
        int dstA = dst[3];
        int outA = srcA + dstA * (255 - srcA) / 255;
        if (outA == 0) return;
        dst[0] = (byte)((srcB * srcA + dst[0] * dstA * (255 - srcA) / 255) / outA);
        dst[1] = (byte)((srcG * srcA + dst[1] * dstA * (255 - srcA) / 255) / outA);
        dst[2] = (byte)((srcR * srcA + dst[2] * dstA * (255 - srcA) / 255) / outA);
        dst[3] = (byte)outA;
    }

    // ピクセルをゼロにする（消しゴム）
    internal static unsafe void ErasePixel(byte* dst, byte alpha)
    {
        int newA = dst[3] * (255 - alpha) / 255;
        int oldA = Math.Max(1, (int)dst[3]);
        dst[0] = (byte)(dst[0] * newA / oldA);
        dst[1] = (byte)(dst[1] * newA / oldA);
        dst[2] = (byte)(dst[2] * newA / oldA);
        dst[3] = (byte)newA;
    }

    // 丸ブラシスタンプ（アンチエイリアス無効）
    internal static Int32Rect StampRound(
        WriteableBitmap bmp, int cx, int cy, int radius,
        byte r, byte g, byte b, byte a, bool erase = false)
    {
        int x0 = Math.Max(0, cx - radius);
        int y0 = Math.Max(0, cy - radius);
        int x1 = Math.Min(bmp.PixelWidth - 1, cx + radius);
        int y1 = Math.Min(bmp.PixelHeight - 1, cy + radius);
        if (x0 > x1 || y0 > y1) return default;

        bmp.Lock();
        unsafe
        {
            byte* ptr = (byte*)bmp.BackBuffer;
            int stride = bmp.BackBufferStride;
            int r2 = radius * radius;

            for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
            {
                int dx = px - cx, dy = py - cy;
                if (dx * dx + dy * dy > r2) continue;
                byte* pixel = ptr + py * stride + px * 4;
                if (erase) ErasePixel(pixel, a);
                else BlendPixel(pixel, r, g, b, a);
            }
        }
        var dirty = new Int32Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        bmp.AddDirtyRect(dirty);
        bmp.Unlock();
        return dirty;
    }

    // 四角ブラシスタンプ
    internal static Int32Rect StampSquare(
        WriteableBitmap bmp, int cx, int cy, int half,
        byte r, byte g, byte b, byte a, bool erase = false)
    {
        int x0 = Math.Max(0, cx - half);
        int y0 = Math.Max(0, cy - half);
        int x1 = Math.Min(bmp.PixelWidth - 1, cx + half);
        int y1 = Math.Min(bmp.PixelHeight - 1, cy + half);
        if (x0 > x1 || y0 > y1) return default;

        bmp.Lock();
        unsafe
        {
            byte* ptr = (byte*)bmp.BackBuffer;
            int stride = bmp.BackBufferStride;

            for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
            {
                byte* pixel = ptr + py * stride + px * 4;
                if (erase) ErasePixel(pixel, a);
                else BlendPixel(pixel, r, g, b, a);
            }
        }
        var dirty = new Int32Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        bmp.AddDirtyRect(dirty);
        bmp.Unlock();
        return dirty;
    }

    // 2点間を補間してスタンプ（ストロークの連続描画）
    internal static Int32Rect InterpolateStamp(
        WriteableBitmap bmp,
        double x0, double y0, double x1, double y1, int radius,
        byte r, byte g, byte b, byte a, bool round, bool erase = false)
    {
        double dx = x1 - x0, dy = y1 - y0;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        int steps = Math.Max(1, (int)(dist / Math.Max(1, radius / 2.0)));

        var totalDirty = new Int32Rect(int.MaxValue, int.MaxValue, 0, 0);
        for (int i = 0; i <= steps; i++)
        {
            double t = steps == 0 ? 0 : (double)i / steps;
            int cx = (int)(x0 + dx * t);
            int cy = (int)(y0 + dy * t);
            var d = round
                ? StampRound(bmp, cx, cy, radius, r, g, b, a, erase)
                : StampSquare(bmp, cx, cy, radius, r, g, b, a, erase);
            totalDirty = UnionRect(totalDirty, d);
        }
        return totalDirty;
    }

    internal static Int32Rect UnionRect(Int32Rect a, Int32Rect b)
    {
        if (a.Width == 0) return b;
        if (b.Width == 0) return a;
        int x0 = Math.Min(a.X, b.X);
        int y0 = Math.Min(a.Y, b.Y);
        int x1 = Math.Max(a.X + a.Width, b.X + b.Width);
        int y1 = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return new Int32Rect(x0, y0, x1 - x0, y1 - y0);
    }

    // ピクセル色の取得（Bgra32）
    internal static (byte r, byte g, byte b, byte a) GetPixel(WriteableBitmap bmp, int x, int y)
    {
        if (x < 0 || x >= bmp.PixelWidth || y < 0 || y >= bmp.PixelHeight)
            return (0, 0, 0, 0);
        bmp.Lock();
        byte br, bg, bb, ba;
        unsafe
        {
            byte* ptr = (byte*)bmp.BackBuffer + y * bmp.BackBufferStride + x * 4;
            bb = ptr[0]; bg = ptr[1]; br = ptr[2]; ba = ptr[3];
        }
        bmp.Unlock();
        return (br, bg, bb, ba);
    }
}
