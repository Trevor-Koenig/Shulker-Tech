namespace ShulkerTech.Core.Models;

public class ServerPingLog
{
    public long Id { get; set; }
    public int ServerId { get; set; }
    public MinecraftServer Server { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public bool IsOnline { get; set; }
    public int PlayersOnline { get; set; }
    public int PlayersMax { get; set; }
}
