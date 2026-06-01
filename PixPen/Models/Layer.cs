using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace PixPen.Models;

public class Layer
{
    public string Name { get; set; } = "Layer";
    public bool IsVisible { get; set; } = true;
    public double Opacity { get; set; } = 1.0;
    // BlendMode は将来拡張用（現在は Normal のみ）
    // public BlendMode BlendMode { get; set; } = BlendMode.Normal;

    [JsonIgnore]
    public WriteableBitmap? Bitmap { get; set; }

    [JsonIgnore]
    public int Width => Bitmap?.PixelWidth ?? 0;

    [JsonIgnore]
    public int Height => Bitmap?.PixelHeight ?? 0;
}
