using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Tests.Infrastructure;

/// <summary>Helpers for seeding test data in integration tests.</summary>
public static class TestDbHelper
{
    /// <summary>Creates a MinecraftServer with a unique API key and returns it.</summary>
    public static async Task<MinecraftServer> CreateServerAsync(
        ApplicationDbContext db,
        string? apiKey = null,
        bool isActive = true)
    {
        var server = new MinecraftServer
        {
            Name = $"Test Server {Guid.NewGuid():N}",
            ApiKey = apiKey ?? Guid.NewGuid().ToString("N"),
            IsActive = isActive,
        };
        db.MinecraftServers.Add(server);
        await db.SaveChangesAsync();
        return server;
    }

    /// <summary>Creates an ApplicationUser with optional admin flag and Minecraft UUID.</summary>
    public static async Task<ApplicationUser> CreateUserAsync(
        IServiceProvider services,
        string? email = null,
        string? password = null,
        bool isAdmin = false,
        string? minecraftUuid = null,
        string? role = null)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        email ??= $"test-{Guid.NewGuid():N}@example.com";
        password ??= "Test@1234!";

        var user = new ApplicationUser
        {
            IsAdmin = isAdmin,
            MinecraftUuid = minecraftUuid,
        };

        var userStore = services.GetRequiredService<IUserStore<ApplicationUser>>();
        var emailStore = (IUserEmailStore<ApplicationUser>)userStore;
        await userStore.SetUserNameAsync(user, email, CancellationToken.None);
        await emailStore.SetEmailAsync(user, email, CancellationToken.None);

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        // Confirm email so the user can sign in (RequireConfirmedAccount = true in production config)
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await userManager.ConfirmEmailAsync(user, token);

        if (role != null)
            await userManager.AddToRoleAsync(user, role);

        return user;
    }

    /// <summary>Creates an open PlayerSession (no LeftAt set).</summary>
    public static async Task<PlayerSession> CreateOpenSessionAsync(
        ApplicationDbContext db,
        string userId,
        int serverId,
        DateTime? joinedAt = null)
    {
        var session = new PlayerSession
        {
            UserId = userId,
            ServerId = serverId,
            JoinedAt = joinedAt ?? DateTime.UtcNow,
        };
        db.PlayerSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    /// <summary>Creates a valid single-use invite code and returns the code string.</summary>
    public static async Task<string> CreateInviteCodeAsync(ApplicationDbContext db, int maxUses = 10)
    {
        var code = Guid.NewGuid().ToString("N").ToUpper()[..8];
        db.InviteCodes.Add(new InviteCode { Code = code, MaxUses = maxUses });
        await db.SaveChangesAsync();
        return code;
    }

    /// <summary>Creates a published Article with a unique slug.</summary>
    public static async Task<Article> CreateArticleAsync(
        ApplicationDbContext db,
        string authorId,
        string? title = null,
        string? slug = null,
        bool isPublished = true,
        string? viewRole = null,
        string? editRole = null)
    {
        title ??= $"Test Article {Guid.NewGuid():N}";
        slug ??= title.ToLowerInvariant().Replace(" ", "-");

        var article = new Article
        {
            Title = title,
            Slug = slug,
            Content = "## Test Content\n\nThis is test content.",
            IsPublished = isPublished,
            AuthorId = authorId,
            ViewRole = viewRole,
            EditRole = editRole,
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article;
    }
}
