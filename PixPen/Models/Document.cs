using System.Text.Json.Serialization;

namespace PixPen.Models;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Width { get; set; } = 1000;
    public int Height { get; set; } = 1000;
    public int Dpi { get; set; } = 96;
    public string Title { get; set; } = "Untitled";
    public string? FilePath { get; set; }

    [JsonIgnore]
    public bool IsModified { get; set; }

    public List<Layer> Layers { get; set; } = new();
    public ColorPalette Palette { get; set; } = new();
    public List<PenDefinition> Pens { get; set; } = new();
    public GridSettings Grid { get; set; } = new();
}
