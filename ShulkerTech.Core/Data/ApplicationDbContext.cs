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
    public DbSet<ArticleRevision> ArticleRevisions => Set<ArticleRevision>();
    public DbSet<WikiSettings> WikiSettings => Set<WikiSettings>();
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();
    public DbSet<SecuritySettings> SecuritySettings => Set<SecuritySettings>();
    public DbSet<MinecraftServer> MinecraftServers => Set<MinecraftServer>();
    public DbSet<PlayerSession> PlayerSessions => Set<PlayerSession>();
    public DbSet<ServerPingLog> ServerPingLogs => Set<ServerPingLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Article>()
            .HasIndex(a => a.Slug)
            .IsUnique();

        builder.Entity<ArticleRevision>()
            .HasOne(r => r.Article)
            .WithMany()
            .HasForeignKey(r => r.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ArticleRevision>()
            .HasOne(r => r.Editor)
            .WithMany()
            .HasForeignKey(r => r.EditorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ArticleRevision>()
            .HasIndex(r => new { r.ArticleId, r.EditedAt });

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

        builder.Entity<ServerPingLog>()
            .HasOne(l => l.Server)
            .WithMany()
            .HasForeignKey(l => l.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ServerPingLog>()
            .HasIndex(l => new { l.ServerId, l.Timestamp });

        // Seed a single WikiSettings row with sensible defaults
        builder.Entity<WikiSettings>().HasData(new WikiSettings { Id = 1 });

        // Seed a single SiteSettings row with sensible defaults
        builder.Entity<SiteSettings>().HasData(new SiteSettings { Id = 1 });

        // Seed a single SecuritySettings row
        builder.Entity<SecuritySettings>().HasData(new SecuritySettings { Id = 1 });
    }
}
