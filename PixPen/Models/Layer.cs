using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace PixPen.Models;

public class Layer
{
    public string Name { get; set; } = "Layer";
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public double Opacity { get; set; } = 1.0;
    // BlendMode は将来拡張用（現在は Normal のみ）
    // public BlendMode BlendMode { get; set; } = BlendMode.Normal;

    [JsonIgnore]
    public WriteableBitmap? Bitmap { get; set; }

    // ─── 参照レイヤー専用プロパティ ──────────────────────────────────────
    /// <summary>参照レイヤー（下書き・資料表示用）かどうか</summary>
    public bool IsReference { get; set; } = false;

    /// <summary>参照レイヤーの元画像（キャンバスサイズに依存しない）</summary>
    [JsonIgnore]
    public WriteableBitmap? ReferenceSource { get; set; }

    /// <summary>参照レイヤーのキャンバス上 X 座標（px）</summary>
    public double RefX { get; set; } = 0;
    /// <summary>参照レイヤーのキャンバス上 Y 座標（px）</summary>
    public double RefY { get; set; } = 0;
    /// <summary>参照レイヤーの表示幅（0 = 原寸）</summary>
    public double RefWidth { get; set; } = 0;
    /// <summary>参照レイヤーの表示高さ（0 = 原寸）</summary>
    public double RefHeight { get; set; } = 0;

    /// <summary>実効表示幅</summary>
    [JsonIgnore]
    public double EffectiveRefWidth
        => RefWidth > 0 ? RefWidth : (ReferenceSource?.PixelWidth ?? 0);
    /// <summary>実効表示高さ</summary>
    [JsonIgnore]
    public double EffectiveRefHeight
        => RefHeight > 0 ? RefHeight : (ReferenceSource?.PixelHeight ?? 0);

    [JsonIgnore]
    public int Width => Bitmap?.PixelWidth ?? 0;

    [JsonIgnore]
    public int Height => Bitmap?.PixelHeight ?? 0;
}
