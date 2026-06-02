using System.IO;
using System.Text.Json;

namespace PixPen.Services;

/// <summary>
/// カラーパレットのプリセットを %AppData%\PixPen\palettes\ に保存・管理する。
/// ドキュメントのパレットとは独立した "ユーザーパレットライブラリ" として機能する。
/// </summary>
public class PaletteService
{
    private static readonly string PalettesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PixPen", "palettes");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>保存済みパレット名の一覧（ファイル名から拡張子を除いたもの）</summary>
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

    /// <summary>パレット（色の一覧）をプリセットとして保存する</summary>
    public void Save(string name, IEnumerable<string> hexColors)
    {
        EnsureDirectory();
        var path = FilePath(name);
        var json = JsonSerializer.Serialize(hexColors.ToList(), JsonOptions);
        File.WriteAllText(path, json);
        Logger.Info($"PaletteService: パレット保存 '{name}'");
    }

    /// <summary>プリセットを読み込んで色一覧を返す</summary>
    public IReadOnlyList<string> Load(string name)
    {
        var path = FilePath(name);
        if (!File.Exists(path)) return Array.Empty<string>();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }
        catch (Exception ex)
        {
            Logger.Error($"PaletteService: パレット読み込み失敗 '{name}'", ex);
            return Array.Empty<string>();
        }
    }

    public void Delete(string name)
    {
        var path = FilePath(name);
        if (File.Exists(path)) File.Delete(path);
        Logger.Info($"PaletteService: パレット削除 '{name}'");
    }

    public bool Exists(string name) => File.Exists(FilePath(name));

    private static void EnsureDirectory() => Directory.CreateDirectory(PalettesDir);
    private static string FilePath(string name) => Path.Combine(PalettesDir, $"{name}.json");
}
