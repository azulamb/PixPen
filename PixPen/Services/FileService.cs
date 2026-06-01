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

    // ドキュメントを .ppx (無圧縮ZIP) として保存
    public void Save(Document doc, string path)
    {
        if (File.Exists(path)) File.Delete(path);

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

        // document.json
        var meta = new DocumentMeta
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

        var metaEntry = zip.CreateEntry("document.json", CompressionLevel.NoCompression);
        using (var stream = metaEntry.Open())
        {
            JsonSerializer.Serialize(stream, meta, JsonOptions);
        }

        // 各レイヤーの PNG
        for (int i = 0; i < doc.Layers.Count; i++)
        {
            var layer = doc.Layers[i];
            var entry = zip.CreateEntry($"layer_{i:D4}.png", CompressionLevel.NoCompression);
            using var stream = entry.Open();
            SaveBitmapToPng(layer.Bitmap!, stream);
        }

        doc.FilePath = path;
        doc.IsModified = false;
    }

    public Document Load(string path)
    {
        using var zip = ZipFile.OpenRead(path);

        // document.json を読む
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

        // レイヤーを読む
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
                layer.Bitmap = CreateTransparentBitmap(meta.Width, meta.Height, meta.Dpi);
            }

            doc.Layers.Add(layer);
        }

        return doc;
    }

    // PNG エクスポート（全レイヤー合成）
    public void ExportMerged(Document doc, string path)
    {
        var merged = CreateTransparentBitmap(doc.Width, doc.Height, doc.Dpi);
        CompositeLayersTo(doc, merged);
        using var stream = File.OpenWrite(path);
        SaveBitmapToPng(merged, stream);
    }

    // PNG エクスポート（可視レイヤー個別）
    public void ExportLayersToFolder(Document doc, string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        for (int i = 0; i < doc.Layers.Count; i++)
        {
            var layer = doc.Layers[i];
            if (!layer.IsVisible || layer.Bitmap == null) continue;
            var safeName = string.Concat(layer.Name.Select(c =>
                Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var file = Path.Combine(folderPath, $"{i:D4}_{safeName}.png");
            using var stream = File.OpenWrite(file);
            SaveBitmapToPng(layer.Bitmap, stream);
        }
    }

    public static WriteableBitmap CreateTransparentBitmap(int width, int height, int dpi)
    {
        var bmp = new WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, null);
        bmp.Lock();
        unsafe
        {
            var ptr = (byte*)bmp.BackBuffer;
            int size = bmp.BackBufferStride * height;
            new Span<byte>(ptr, size).Clear();
        }
        bmp.AddDirtyRect(new Int32Rect(0, 0, width, height));
        bmp.Unlock();
        return bmp;
    }

    private static void SaveBitmapToPng(WriteableBitmap bmp, Stream stream)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        encoder.Save(stream);
    }

    private static WriteableBitmap LoadBitmapFromPng(Stream stream, int docWidth, int docHeight, int dpi)
    {
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
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
                {
                    for (int x = 0; x < Math.Min(w, layer.Width); x++)
                    {
                        byte* src = srcPtr + y * srcStride + x * 4;
                        byte* dst = dstPtr + y * stride + x * 4;
                        AlphaComposite(src, dst, opacity);
                    }
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

    // JSON シリアライズ用中間クラス
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
