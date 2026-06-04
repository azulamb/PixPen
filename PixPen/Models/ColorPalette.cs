namespace PixPen.Models;

public class ColorPalette
{
    public List<string> Colors { get; set; } = new();
    public string ForegroundColor { get; set; } = "#FF141820";
    public string BackgroundColor { get; set; } = "#FFF5F8FF";

    /// <summary>デフォルトの 20 色（基本パレット）</summary>
    public static IReadOnlyList<string> DefaultColors { get; } = new[]
    {
        "#FFF5F8FF",   //  1. ほぼ白
        "#FF141820",   //  2. ほぼ黒
        "#FFD44444",   //  3. ソフトレッド
        "#FFD06070",   //  4. ローズ
        "#FFD06030",   //  5. バーントオレンジ
        "#FFD0A030",   //  6. アンバー
        "#FFC8C030",   //  7. オリーブイエロー
        "#FF80B840",   //  8. ソフトライム
        "#FF40A858",   //  9. フォレストグリーン
        "#FF30A888",   // 10. ティール
        "#FF3090B8",   // 11. スカイブルー
        "#FF3050A8",   // 12. ソフトネイビー
        "#FF5840A8",   // 13. ブルーバイオレット
        "#FF8840A8",   // 14. ソフトパープル
        "#FFC04090",   // 15. ソフトマゼンタ
        "#FFD0D4DC",   // 16. ライトクールグレー
        "#FF8890A0",   // 17. ミディアムクールグレー
        "#FF404858",   // 18. ダーククールグレー
        "#FFD0B888",   // 19. ウォームサンド
        "#FF806040",   // 20. ウォームブラウン
    };

    public static string DefaultForegroundColor => "#FF141820";
    public static string DefaultBackgroundColor => "#FFF5F8FF";

    public ColorPalette()
    {
        Colors.AddRange(DefaultColors);
    }
}
