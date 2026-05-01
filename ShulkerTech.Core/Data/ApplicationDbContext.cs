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
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArticleFavorite> ArticleFavorites => Set<ArticleFavorite>();
    public DbSet<ArticleRating> ArticleRatings => Set<ArticleRating>();
    public DbSet<ArticleTemplate> ArticleTemplates => Set<ArticleTemplate>();
    public DbSet<WikiSettings> WikiSettings => Set<WikiSettings>();
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();
    public DbSet<SecuritySettings> SecuritySettings => Set<SecuritySettings>();
    public DbSet<MinecraftServer> MinecraftServers => Set<MinecraftServer>();
    public DbSet<PlayerSession> PlayerSessions => Set<PlayerSession>();
    public DbSet<ServerPingLog> ServerPingLogs => Set<ServerPingLog>();
    public DbSet<SitePermission> SitePermissions => Set<SitePermission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Article>()
            .HasIndex(a => a.Slug)
            .IsUnique();

        builder.Entity<Tag>()
            .HasIndex(t => t.Slug)
            .IsUnique();

        builder.Entity<Article>()
            .HasMany(a => a.Tags)
            .WithMany(t => t.Articles)
            .UsingEntity("ArticleTags");

        builder.Entity<Tag>().HasData(
            new Tag { Id = 1,  Name = "Getting Started", Slug = "getting-started", Icon = "🗺️", Color = "var(--color-plasma)" },
            new Tag { Id = 2,  Name = "Server Info",     Slug = "server-info",     Icon = "📋", Color = "var(--color-crystal)" },
            new Tag { Id = 3,  Name = "Survival",        Slug = "survival",        Icon = "⛏️", Color = "var(--color-rune)" },
            new Tag { Id = 4,  Name = "Redstone",        Slug = "redstone",        Icon = "⚡", Color = "var(--color-redstone)" },
            new Tag { Id = 5,  Name = "Farms",           Slug = "farms",           Icon = "🥕", Color = "#f97316" },
            new Tag { Id = 6,  Name = "Building",        Slug = "building",        Icon = "🏗️", Color = "#a78bfa" },
            new Tag { Id = 7,  Name = "Events",          Slug = "events",          Icon = "🎉", Color = "#ec4899" },
            new Tag { Id = 8,  Name = "Community",       Slug = "community",       Icon = "👥", Color = "#22d3ee" },
            new Tag { Id = 9,  Name = "Rules",           Slug = "rules",           Icon = "📜", Color = "#facc15" },
            new Tag { Id = 10, Name = "Lore",            Slug = "lore",            Icon = "📖", Color = "#84cc16" },
            new Tag { Id = 11, Name = "Economy",         Slug = "economy",         Icon = "💰", Color = "#fbbf24" },
            new Tag { Id = 12, Name = "PvP",             Slug = "pvp",             Icon = "⚔️", Color = "#ef4444" }
        );

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

        builder.Entity<ArticleFavorite>()
            .HasKey(f => new { f.UserId, f.ArticleId });

        builder.Entity<ArticleFavorite>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ArticleFavorite>()
            .HasOne(f => f.Article)
            .WithMany(a => a.Favorites)
            .HasForeignKey(f => f.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ArticleRating>()
            .HasIndex(r => new { r.ArticleId, r.UserId })
            .IsUnique();

        builder.Entity<ArticleRating>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ArticleRating>()
            .HasOne(r => r.Article)
            .WithMany()
            .HasForeignKey(r => r.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

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

        builder.Entity<SitePermission>()
            .HasIndex(p => new { p.RoleName, p.Resource })
            .IsUnique();

        // Default permission grants — Member: create + edit own; Moderator: also edit any + delete
        builder.Entity<SitePermission>().HasData(
            new SitePermission { Id = 1, RoleName = "Member",    Resource = "wiki.create"   },
            new SitePermission { Id = 2, RoleName = "Member",    Resource = "wiki.edit_own" },
            new SitePermission { Id = 3, RoleName = "Moderator", Resource = "wiki.create"   },
            new SitePermission { Id = 4, RoleName = "Moderator", Resource = "wiki.edit_own" },
            new SitePermission { Id = 5, RoleName = "Moderator", Resource = "wiki.edit_any" },
            new SitePermission { Id = 6, RoleName = "Moderator", Resource = "wiki.delete"   }
        );

        // Seed a single WikiSettings row with sensible defaults
        builder.Entity<WikiSettings>().HasData(new WikiSettings { Id = 1 });

        // Seed a single SiteSettings row with sensible defaults
        builder.Entity<SiteSettings>().HasData(new SiteSettings { Id = 1 });

        // Seed a single SecuritySettings row
        builder.Entity<SecuritySettings>().HasData(new SecuritySettings { Id = 1 });
    }
}
