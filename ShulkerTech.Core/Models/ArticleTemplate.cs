namespace ShulkerTech.Core.Models;

public class ArticleTemplate
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string Content { get; set; } = "";
    /// <summary>Auto-loaded on the Create page when the editor is empty.</summary>
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
