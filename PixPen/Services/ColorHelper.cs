using System.Windows.Media;

namespace PixPen.Services;

public static class ColorHelper
{
    public static Color FromArgbHex(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.Black; }
    }

    public static string ToArgbHex(Color color)
        => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    public static (double h, double s, double v) ToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0;
        if (delta > 0)
        {
            if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * ((b - r) / delta + 2);
            else h = 60 * ((r - g) / delta + 4);
            if (h < 0) h += 360;
        }
        double s = max == 0 ? 0 : delta / max;
        return (h, s, max);
    }

    public static Color FromHsv(double h, double s, double v, byte alpha = 255)
    {
        double r, g, b;
        if (s == 0) { r = g = b = v; }
        else
        {
            h = ((h % 360) + 360) % 360;
            int i = (int)(h / 60);
            double f = h / 60 - i;
            double p = v * (1 - s);
            double q = v * (1 - s * f);
            double t = v * (1 - s * (1 - f));
            (r, g, b) = i switch
            {
                0 => (v, t, p), 1 => (q, v, p),
                2 => (p, v, t), 3 => (p, q, v),
                4 => (t, p, v), _ => (v, p, q)
            };
        }
        return Color.FromArgb(alpha, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
