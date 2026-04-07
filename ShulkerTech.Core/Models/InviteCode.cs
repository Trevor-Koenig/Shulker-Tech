namespace ShulkerTech.Core.Models;

public class InviteCode
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public string? Note { get; set; }            // e.g. "Wave 3 - Discord invite"
    public int MaxUses { get; set; } = 1;
    public int UseCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;

    public bool IsValid =>
        !IsRevoked &&
        UseCount < MaxUses &&
        (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}
