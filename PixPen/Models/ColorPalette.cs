namespace PixPen.Models;

public class ColorPalette
{
    public List<string> Colors { get; set; } = new();
    public string ForegroundColor { get; set; } = "#FF000000";
    public string BackgroundColor { get; set; } = "#FFFFFFFF";

    public ColorPalette()
    {
        Colors.AddRange(new[]
        {
            "#FF000000", "#FFFFFFFF", "#FFFF0000", "#FF00FF00", "#FF0000FF",
            "#FFFFFF00", "#FF00FFFF", "#FFFF00FF", "#FFFF8000", "#FF8000FF",
            "#FFFF8080", "#FF80FF80", "#FF8080FF", "#FF808080", "#FFC0C0C0",
            "#FF400000", "#FF004000", "#FF000040", "#FF404000", "#FF004040",
        });
    }
}
