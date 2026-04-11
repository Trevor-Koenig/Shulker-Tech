namespace ShulkerTech.Core.Models;

public class MinecraftServer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; } = 25565;
    public bool IsActive { get; set; } = true;
    public required string ApiKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlayerSession> PlayerSessions { get; set; } = [];
}
