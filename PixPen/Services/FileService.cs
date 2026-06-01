using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixPen.Models;

namespace PixPen.Services;

public class FileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // ─── 保存 ──────────────────────────────────────────────────────────────

    /// <summary>ドキュメントを .ppx（無圧縮 ZIP）として保存する</summary>
    public void Save(Document doc, string path)
    {
        // 一時ファイルに書いてから移動（書き込み途中クラッシュで元ファイルを壊さないため）
        var tmpPath = path + ".tmp";
        try
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);

            using (var zip = ZipFile.Open(tmpPath, ZipArchiveMode.Create))
            {
                // document.json
                var meta = BuildMeta(doc);
                var metaEntry = zip.CreateEntry("document.json", CompressionLevel.NoCompression);
                using (var stream = metaEntry.Open())
                    JsonSerializer.Serialize(stream, meta, JsonOptions);

                // 各レイヤーの PNG
                for (int i = 0; i < doc.Layers.Count; i++)
                {
                    var layer = doc.Layers[i];
                    if (layer.Bitmap == null)
                    {
                        Logger.Warn($"Save: layer[{i}] '{layer.Name}' の Bitmap が null のためスキップ");
                        continue;
                    }
                    var entry = zip.CreateEntry($"layer_{i:D4}.png", CompressionLevel.NoCompression);
                    using var stream = entry.Open();
                    SaveBitmapToPng(layer.Bitmap, stream);
                }
            }

            // 成功したら正式パスに移動
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmpPath, path);

            doc.FilePath = path;
            doc.IsModified = false;
            Logger.Info($"Save: 保存完了 '{path}'");
        }
        catch (Exception ex)
        {
            Logger.Error($"Save: 保存失敗 '{path}'", ex);
            // 一時ファイルのクリーンアップ
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
            throw;
        }
    }

    // ─── 読み込み ────────────────────────────────────────────────────────────

    /// <summary>.ppx ファイルを読み込んで Document を返す</summary>
    public Document Load(string path)
    {
        Logger.Info($"Load: 読み込み開始 '{path}'");
        try
        {
            using var zip = ZipFile.OpenRead(path);

            var metaEntry = zip.GetEntry("document.json")
                ?? throw new InvalidDataException("document.json が見つかりません");

            DocumentMeta meta;
            using (var stream = metaEntry.Open())
            {
                meta = JsonSerializer.Deserialize<DocumentMeta>(stream, JsonOptions)
                    ?? throw new InvalidDataException("document.json の解析に失敗しました");
            }

            var doc = new Document
            {
                Id = meta.Id,
                Width = meta.Width,
                Height = meta.Height,
                Dpi = meta.Dpi,
                Title = meta.Title,
                FilePath = path,
                Palette = meta.Palette,
                Pens = meta.Pens,
                Grid = meta.Grid,
                IsModified = false
            };

            foreach (var lm in meta.Layers.OrderBy(l => l.Index))
            {
                var layer = new Layer
                {
                    Name = lm.Name,
                    IsVisible = lm.IsVisible,
                    Opacity = lm.Opacity
                };

                var imgEntry = zip.GetEntry($"layer_{lm.Index:D4}.png");
                if (imgEntry != null)
                {
                    using var stream = imgEntry.Open();
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    ms.Position = 0;
                    layer.Bitmap = LoadBitmapFromPng(ms, meta.Width, meta.Height, meta.Dpi);
                }
                else
                {
                    Logger.Warn($"Load: layer_{lm.Index:D4}.png が見つかりません。透明ビットマップで代替");
                    layer.Bitmap = CreateTransparentBitmap(meta.Width, meta.Height, meta.Dpi);
                }

                doc.Layers.Add(layer);
            }

            Logger.Info($"Load: 読み込み完了 '{path}' ({doc.Layers.Count} レイヤー)");
            return doc;
        }
        catch (Exception ex)
        {
            Logger.Error($"Load: 読み込み失敗 '{path}'", ex);
            throw;
        }
    }

    // ─── エクスポート ─────────────────────────────────────────────────────────

    public void ExportMerged(Document doc, string path)
    {
        try
        {
            var merged = CreateTransparentBitmap(doc.Width, doc.Height, doc.Dpi);
            CompositeLayersTo(doc, merged);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            SaveBitmapToPng(merged, stream);
            Logger.Info($"ExportMerged: エクスポート完了 '{path}'");
        }
        catch (Exception ex)
        {
            Logger.Error($"ExportMerged: 失敗 '{path}'", ex);
            throw;
        }
    }

    public void ExportLayersToFolder(Document doc, string folderPath)
    {
        try
        {
            Directory.CreateDirectory(folderPath);
            int exported = 0;
            for (int i = 0; i < doc.Layers.Count; i++)
            {
                var layer = doc.Layers[i];
                if (!layer.IsVisible || layer.Bitmap == null) continue;
                var safeName = string.Concat(layer.Name.Select(c =>
                    Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                var file = Path.Combine(folderPath, $"{i:D4}_{safeName}.png");
                using var stream = new FileStream(file, FileMode.Create, FileAccess.Write);
                SaveBitmapToPng(layer.Bitmap, stream);
                exported++;
            }
            Logger.Info($"ExportLayersToFolder: {exported} レイヤーをエクスポート → '{folderPath}'");
        }
        catch (Exception ex)
        {
            Logger.Error($"ExportLayersToFolder: 失敗 '{folderPath}'", ex);
            throw;
        }
    }

    // ─── ビットマップ操作 ─────────────────────────────────────────────────────

    public static WriteableBitmap CreateTransparentBitmap(int width, int height, int dpi)
    {
        var bmp = new WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, null);
        bmp.Lock();
        unsafe
        {
            var ptr = (byte*)bmp.BackBuffer;
            new Span<byte>(ptr, bmp.BackBufferStride * height).Clear();
        }
        bmp.AddDirtyRect(new Int32Rect(0, 0, width, height));
        bmp.Unlock();
        return bmp;
    }

    /// <summary>
    /// WriteableBitmap を PNG としてストリームに書き出す。
    /// 2 段階方式:
    ///   1. ピクセルデータを配列にコピー → Freeze 可能な BitmapSource 経由でエンコード
    ///      （WriteableBitmap は Freeze 不可のため直接エンコードできない）
    ///   2. MemoryStream に書いてから宛先ストリームにコピー
    ///      （ZipArchive の Entry ストリームはシーク不可のため PngBitmapEncoder が直接書けない）
    /// </summary>
    private static void SaveBitmapToPng(WriteableBitmap bmp, Stream destination)
    {
        int w = bmp.PixelWidth;
        int h = bmp.PixelHeight;
        int stride = w * ((bmp.Format.BitsPerPixel + 7) / 8);
        var pixels = new byte[stride * h];

        // 1. ピクセルをコピー（WriteableBitmap → 配列）
        bmp.CopyPixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);

        // 2. Freeze 可能な BitmapSource を作成
        var source = BitmapSource.Create(w, h, bmp.DpiX, bmp.DpiY,
            bmp.Format, bmp.Palette, pixels, stride);

        // 3. MemoryStream に PNG を書き出す（シーク可能なので PngBitmapEncoder が使える）
        using var ms = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(ms);

        // 4. MemoryStream の内容を宛先に書き込む
        ms.Position = 0;
        ms.CopyTo(destination);
    }

    private static WriteableBitmap LoadBitmapFromPng(Stream stream, int docWidth, int docHeight, int dpi)
    {
        var decoder = new PngBitmapDecoder(stream,
            BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var src = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        var wb = new WriteableBitmap(converted);
        return wb;
    }

    private static void CompositeLayersTo(Document doc, WriteableBitmap dest)
    {
        int w = dest.PixelWidth, h = dest.PixelHeight;
        dest.Lock();
        unsafe
        {
            byte* dstPtr = (byte*)dest.BackBuffer;
            int stride = dest.BackBufferStride;
            new Span<byte>(dstPtr, stride * h).Clear();

            foreach (var layer in Enumerable.Reverse(doc.Layers))
            {
                if (!layer.IsVisible || layer.Bitmap == null) continue;
                layer.Bitmap.Lock();
                byte* srcPtr = (byte*)layer.Bitmap.BackBuffer;
                int srcStride = layer.Bitmap.BackBufferStride;
                byte opacity = (byte)(layer.Opacity * 255);

                for (int y = 0; y < Math.Min(h, layer.Height); y++)
                for (int x = 0; x < Math.Min(w, layer.Width); x++)
                {
                    byte* src = srcPtr + y * srcStride + x * 4;
                    byte* dst = dstPtr + y * stride + x * 4;
                    AlphaComposite(src, dst, opacity);
                }
                layer.Bitmap.Unlock();
            }
        }
        dest.AddDirtyRect(new Int32Rect(0, 0, w, h));
        dest.Unlock();
    }

    internal static unsafe void AlphaComposite(byte* src, byte* dst, byte layerOpacity)
    {
        int srcA = src[3] * layerOpacity / 255;
        if (srcA == 0) return;
        int dstA = dst[3];
        int outA = srcA + dstA * (255 - srcA) / 255;
        if (outA == 0) return;
        dst[0] = (byte)((src[0] * srcA + dst[0] * dstA * (255 - srcA) / 255) / outA);
        dst[1] = (byte)((src[1] * srcA + dst[1] * dstA * (255 - srcA) / 255) / outA);
        dst[2] = (byte)((src[2] * srcA + dst[2] * dstA * (255 - srcA) / 255) / outA);
        dst[3] = (byte)outA;
    }

    private static DocumentMeta BuildMeta(Document doc) => new()
    {
        Id = doc.Id,
        Width = doc.Width,
        Height = doc.Height,
        Dpi = doc.Dpi,
        Title = doc.Title,
        Palette = doc.Palette,
        Pens = doc.Pens,
        Grid = doc.Grid,
        Layers = doc.Layers.Select((l, i) => new LayerMeta
        {
            Index = i,
            Name = l.Name,
            IsVisible = l.IsVisible,
            Opacity = l.Opacity
        }).ToList()
    };

    // ─── JSON 中間クラス ──────────────────────────────────────────────────────

    private class DocumentMeta
    {
        public Guid Id { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Dpi { get; set; }
        public string Title { get; set; } = "";
        public ColorPalette Palette { get; set; } = new();
        public List<PenDefinition> Pens { get; set; } = new();
        public GridSettings Grid { get; set; } = new();
        public List<LayerMeta> Layers { get; set; } = new();
    }

    private class LayerMeta
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public bool IsVisible { get; set; }
        public double Opacity { get; set; }
    }
}
