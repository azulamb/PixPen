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

                // 各レイヤーの PNG（通常レイヤー = layer_XXXX.png、参照レイヤー = ref_XXXX.png）
                for (int i = 0; i < doc.Layers.Count; i++)
                {
                    var layer = doc.Layers[i];
                    if (layer.IsReference)
                    {
                        if (layer.ReferenceSource == null) continue;
                        var refEntry = zip.CreateEntry($"ref_{i:D4}.png", CompressionLevel.NoCompression);
                        using var refStream = refEntry.Open();
                        SaveBitmapToPng(layer.ReferenceSource, refStream);
                        continue;
                    }
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
                    Name      = lm.Name,
                    IsVisible = lm.IsVisible,
                    IsLocked  = lm.IsLocked,
                    Opacity   = lm.Opacity,
                    IsReference = lm.IsReference,
                    RefX      = lm.RefX,
                    RefY      = lm.RefY,
                    RefWidth  = lm.RefWidth,
                    RefHeight = lm.RefHeight,
                };

                if (lm.IsReference)
                {
                    // 参照レイヤー: ref_XXXX.png から元画像を読み込む
                    var refEntry = zip.GetEntry($"ref_{lm.Index:D4}.png");
                    if (refEntry != null)
                    {
                        using var stream = refEntry.Open();
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        ms.Position = 0;
                        var decoder = new PngBitmapDecoder(ms,
                            BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        var converted = new FormatConvertedBitmap(
                            decoder.Frames[0], PixelFormats.Bgra32, null, 0);
                        layer.ReferenceSource = new WriteableBitmap(converted);
                    }
                    else
                    {
                        Logger.Warn($"Load: ref_{lm.Index:D4}.png が見つかりません");
                    }
                }
                else
                {
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

    /// <summary>
    /// レイヤービットマップを新サイズにリサイズして返す（左上基準）。
    /// 縮小時は右下をカット、拡大時は右下を透明で拡張する。
    /// </summary>
    public static WriteableBitmap ResizeLayerBitmap(WriteableBitmap source, int newWidth, int newHeight, int dpi)
    {
        var newBmp = CreateTransparentBitmap(newWidth, newHeight, dpi);
        int copyW = Math.Min(source.PixelWidth, newWidth);
        int copyH = Math.Min(source.PixelHeight, newHeight);
        if (copyW <= 0 || copyH <= 0) return newBmp;

        var pixels = new byte[copyW * copyH * 4];
        source.CopyPixels(new Int32Rect(0, 0, copyW, copyH), pixels, copyW * 4, 0);
        newBmp.WritePixels(new Int32Rect(0, 0, copyW, copyH), pixels, copyW * 4, 0);
        return newBmp;
    }

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
    /// 指定色で塗りつぶした WriteableBitmap を作成する。
    /// colorHex は #AARRGGBB 形式。
    /// </summary>
    public static WriteableBitmap CreateFilledBitmap(int width, int height, int dpi, string colorHex)
    {
        // #AARRGGBB をパース（失敗時は白）
        byte a = 0xFF, r = 0xFF, g = 0xFF, b = 0xFF;
        if (colorHex.Length == 9 && colorHex[0] == '#')
        {
            try
            {
                a = Convert.ToByte(colorHex.Substring(1, 2), 16);
                r = Convert.ToByte(colorHex.Substring(3, 2), 16);
                g = Convert.ToByte(colorHex.Substring(5, 2), 16);
                b = Convert.ToByte(colorHex.Substring(7, 2), 16);
            }
            catch { }
        }

        var bmp = new WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, null);
        bmp.Lock();
        unsafe
        {
            byte* ptr = (byte*)bmp.BackBuffer;
            int stride = bmp.BackBufferStride;
            // 1 行目を塗りつぶす
            for (int x = 0; x < width; x++)
            {
                byte* px = ptr + x * 4;
                px[0] = b; px[1] = g; px[2] = r; px[3] = a;
            }
            // 残りの行は 1 行目をコピー
            for (int y = 1; y < height; y++)
                new Span<byte>(ptr, width * 4).CopyTo(new Span<byte>(ptr + y * stride, width * 4));
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
        var fullRect = new Int32Rect(0, 0, w, h);
        dest.Lock();
        unsafe
        {
            byte* dstPtr = (byte*)dest.BackBuffer;
            int stride = dest.BackBufferStride;
            new Span<byte>(dstPtr, stride * h).Clear();

            foreach (var layer in Enumerable.Reverse(doc.Layers))
            {
                if (!layer.IsVisible) continue;

                if (layer.IsReference)
                {
                    // 参照レイヤー: キャンバス範囲にクリップして合成
                    CompositeReferenceLayer(layer, dstPtr, stride, w, h, fullRect);
                    continue;
                }

                if (layer.Bitmap == null) continue;
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
        dest.AddDirtyRect(fullRect);
        dest.Unlock();
    }

    /// <summary>
    /// 参照レイヤーをキャンバス領域に合成する。
    /// ReferenceSource を RefX/RefY/RefWidth/RefHeight の変換でスケーリングして dirtyRect に書き込む。
    /// canvasW/H がキャンバス全体サイズ、dstPtr/dstStride は CompositeBitmap のバッファ。
    /// </summary>
    internal static unsafe void CompositeReferenceLayer(
        Layer layer, byte* dstPtr, int dstStride,
        int canvasW, int canvasH, Int32Rect dirty)
    {
        var src = layer.ReferenceSource;
        if (src == null) return;

        int srcW = src.PixelWidth, srcH = src.PixelHeight;
        double refX = layer.RefX, refY = layer.RefY;
        double refW = layer.EffectiveRefWidth;
        double refH = layer.EffectiveRefHeight;
        if (refW <= 0 || refH <= 0) return;

        // 合成対象の矩形をキャンバス・dirty・参照画像の重なりに絞る
        int minX = Math.Max(dirty.X, Math.Max(0, (int)Math.Floor(refX)));
        int maxX = Math.Min(dirty.X + dirty.Width - 1, Math.Min(canvasW - 1, (int)Math.Ceiling(refX + refW) - 1));
        int minY = Math.Max(dirty.Y, Math.Max(0, (int)Math.Floor(refY)));
        int maxY = Math.Min(dirty.Y + dirty.Height - 1, Math.Min(canvasH - 1, (int)Math.Ceiling(refY + refH) - 1));
        if (minX > maxX || minY > maxY) return;

        byte opacity = (byte)(Math.Clamp(layer.Opacity, 0.0, 1.0) * 255);

        src.Lock();
        try
        {
            byte* srcPtr = (byte*)src.BackBuffer;
            int srcStride = src.BackBufferStride;

            for (int y = minY; y <= maxY; y++)
            {
                // canvas y → reference image y
                double v = (y + 0.5 - refY) / refH * srcH;
                int sj = (int)v;
                if (sj < 0 || sj >= srcH) continue;

                for (int x = minX; x <= maxX; x++)
                {
                    double u = (x + 0.5 - refX) / refW * srcW;
                    int si = (int)u;
                    if (si < 0 || si >= srcW) continue;

                    byte* srcPx = srcPtr + sj * srcStride + si * 4;
                    byte* dstPx = dstPtr + y * dstStride + x * 4;
                    AlphaComposite(srcPx, dstPx, opacity);
                }
            }
        }
        finally { src.Unlock(); }
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
            Index       = i,
            Name        = l.Name,
            IsVisible   = l.IsVisible,
            IsLocked    = l.IsLocked,
            Opacity     = l.Opacity,
            IsReference = l.IsReference,
            RefX        = l.RefX,
            RefY        = l.RefY,
            RefWidth    = l.RefWidth,
            RefHeight   = l.RefHeight,
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
        public bool IsLocked { get; set; }
        public double Opacity { get; set; }
        public bool IsReference { get; set; }
        public double RefX { get; set; }
        public double RefY { get; set; }
        public double RefWidth { get; set; }
        public double RefHeight { get; set; }
    }
}
