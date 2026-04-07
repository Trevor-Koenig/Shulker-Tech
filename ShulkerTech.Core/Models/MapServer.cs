namespace ShulkerTech.Core.Models;

public class MapServer
{
    public int Id { get; set; }
    public required string Label { get; set; }
    public required string Url { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
