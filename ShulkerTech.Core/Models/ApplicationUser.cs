using Microsoft.AspNetCore.Identity;

namespace ShulkerTech.Core.Models;

public class ApplicationUser : IdentityUser
{
    public string? MinecraftUsername { get; set; }
    public string? MinecraftUuid { get; set; }
    public bool IsAdmin { get; set; } = false;
    public bool MustChangePassword { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
