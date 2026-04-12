namespace ShulkerTech.Core.Models;

/// <summary>Single-row settings table for homepage content.</summary>
public class SiteSettings
{
    public int Id { get; set; } = 1;

    public string HeroTagline { get; set; } =
        "A technical Minecraft community — engineered by its players, built to last.";

    public string BuildCardTitle { get; set; } = "BUILD";
    public string BuildCardBody { get; set; } =
        "From mega-farms to redstone contraptions, Shulker Tech celebrates technical Minecraft. Build what others say is impossible.";

    public string ExploreCardTitle { get; set; } = "EXPLORE";
    public string ExploreCardBody { get; set; } =
        "Discover automated systems, player-built infrastructure, and the remnants of seasons past.";

    public string ConnectCardTitle { get; set; } = "CONNECT";
    public string ConnectCardBody { get; set; } =
        "A tight-knit community of builders and tinkerers. Find your people, share your farms, and shape the server together.";
}
