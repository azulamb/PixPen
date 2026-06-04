using System.IO;
using System.Text.Json;
using PixPen.Models;

namespace PixPen.Services;

/// <summary>パレットプリセットの保存データ（前景色・背景色も含む）</summary>
public class PalettePreset
{
    public List<string> Colors { get; set; } = new();
    public string? ForegroundColor { get; set; }
    public string? BackgroundColor { get; set; }
}

/// <summary>
/// カラーパレットのプリセットを %AppData%\PixPen\palettes\ に保存・管理する。
/// </summary>
public class PaletteService
{
    /// <summary>編集・保存できない組み込みパレット名</summary>
    public const string BasicPaletteName = "基本パレット";

    private static readonly string PalettesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PixPen", "palettes");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>保存済みパレット名の一覧（基本パレットは含まない）</summary>
    public IReadOnlyList<string> GetNames()
    {
        try
        {
            EnsureDirectory();
            return Directory.GetFiles(PalettesDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n)
                .ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>パレットを保存する（前景色・背景色も含む）</summary>
    public void Save(string name, PalettePreset preset)
    {
        EnsureDirectory();
        var path = FilePath(name);
        var json = JsonSerializer.Serialize(preset, JsonOptions);
        File.WriteAllText(path, json);
        Logger.Info($"PaletteService: パレット保存 '{name}'");
    }

    /// <summary>プリセットを読み込む。"基本パレット" は組み込みデータを返す。</summary>
    public PalettePreset Load(string name)
    {
        if (name == BasicPaletteName)
        {
            return new PalettePreset
            {
                Colors          = ColorPalette.DefaultColors.ToList(),
                ForegroundColor = ColorPalette.DefaultForegroundColor,
                BackgroundColor = ColorPalette.DefaultBackgroundColor,
            };
        }

        var path = FilePath(name);
        if (!File.Exists(path)) return new PalettePreset();
        try
        {
            var json = File.ReadAllText(path);
            var trimmed = json.TrimStart();

            // 新形式（{...} オブジェクト）か旧形式（[...] 配列）かで分岐
            if (trimmed.StartsWith("{"))
            {
                var preset = JsonSerializer.Deserialize<PalettePreset>(json, JsonOptions);
                if (preset?.Colors.Count > 0) return preset;
            }
            else if (trimmed.StartsWith("["))
            {
                var colors = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
                if (colors?.Count > 0)
                    return new PalettePreset { Colors = colors };
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"PaletteService: パレット読み込み失敗 '{name}'", ex);
        }
        return new PalettePreset();
    }

    public void Delete(string name)
    {
        var path = FilePath(name);
        if (File.Exists(path)) File.Delete(path);
        Logger.Info($"PaletteService: パレット削除 '{name}'");
    }

    public bool Exists(string name)
        => name == BasicPaletteName || File.Exists(FilePath(name));

    private static void EnsureDirectory() => Directory.CreateDirectory(PalettesDir);
    private static string FilePath(string name) => Path.Combine(PalettesDir, $"{name}.json");
}
