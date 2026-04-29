namespace ShulkerTech.Core.Models;

public class ArticleRevision
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? MapUrl { get; set; }

    public string EditorId { get; set; } = "";
    public ApplicationUser Editor { get; set; } = null!;

    public DateTime EditedAt { get; set; }
}
