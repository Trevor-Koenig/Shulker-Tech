namespace ShulkerTech.Core.Models;

public class ArticleRating
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    /// <summary>1–5 redstone dust.</summary>
    public byte Usefulness { get; set; }
    /// <summary>1–5 diamonds.</summary>
    public byte Coolness { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
