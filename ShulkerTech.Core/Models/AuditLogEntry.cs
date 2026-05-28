namespace ShulkerTech.Core.Models;

public class AuditLogEntry
{
    public int Id { get; set; }

    /// <summary>Dot-separated action key, e.g. "article.created".</summary>
    public required string Action { get; set; }

    public required string ActorId { get; set; }
    public ApplicationUser Actor { get; set; } = null!;

    /// <summary>Stored value only — no FK so deletions don't cascade into audit history.</summary>
    public int? ArticleId { get; set; }

    /// <summary>Snapshot of the article title at the time of the action.</summary>
    public string? ArticleTitle { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public static class AuditAction
{
    public const string ArticleCreated = "article.created";
    public const string ArticleUpdated = "article.updated";
    public const string ArticleDeleted = "article.deleted";
}
