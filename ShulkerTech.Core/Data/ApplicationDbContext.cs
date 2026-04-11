using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Core.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<MapServer> MapServers => Set<MapServer>();
    public DbSet<InviteCode> InviteCodes => Set<InviteCode>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<WikiSettings> WikiSettings => Set<WikiSettings>();
    public DbSet<MinecraftServer> MinecraftServers => Set<MinecraftServer>();
    public DbSet<PlayerSession> PlayerSessions => Set<PlayerSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Article>()
            .HasIndex(a => a.Slug)
            .IsUnique();

        builder.Entity<MinecraftServer>()
            .HasIndex(s => s.ApiKey)
            .IsUnique();

        builder.Entity<PlayerSession>()
            .HasOne(ps => ps.User)
            .WithMany()
            .HasForeignKey(ps => ps.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PlayerSession>()
            .HasOne(ps => ps.Server)
            .WithMany(s => s.PlayerSessions)
            .HasForeignKey(ps => ps.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed a single WikiSettings row with sensible defaults
        builder.Entity<WikiSettings>().HasData(new WikiSettings { Id = 1 });
    }
}
