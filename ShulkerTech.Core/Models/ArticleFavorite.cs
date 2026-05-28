namespace ShulkerTech.Core.Models;

public class ArticleFavorite
{
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
