namespace ShulkerTech.Core.Models;

public class Tag
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    /// <summary>Emoji or unicode symbol shown on tag pills. Empty string means no icon.</summary>
    public string Icon { get; set; } = "";
    /// <summary>CSS color value (hex or CSS custom property).</summary>
    public string Color { get; set; } = "var(--color-accent)";
    public string? Description { get; set; }

    public ICollection<Article> Articles { get; set; } = [];
}
