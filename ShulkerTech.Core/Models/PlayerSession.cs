namespace ShulkerTech.Core.Models;

public class PlayerSession
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public int ServerId { get; set; }
    public MinecraftServer Server { get; set; } = null!;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    public long? DurationSeconds { get; set; }

    public TimeSpan? Duration => DurationSeconds.HasValue ? TimeSpan.FromSeconds(DurationSeconds.Value) : null;
}
