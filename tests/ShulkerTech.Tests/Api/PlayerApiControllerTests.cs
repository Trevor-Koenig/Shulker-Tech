using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Api;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class PlayerApiControllerTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient() => factory.CreateClient(
        new() { AllowAutoRedirect = false });

    private IServiceScope CreateScope() => factory.Services.CreateScope();

    [Fact]
    public async Task Join_ValidApiKeyAndKnownUuid_Returns200()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider,
            minecraftUuid: Guid.NewGuid().ToString());

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", server.ApiKey);

        var response = await client.PostAsJsonAsync("/api/player/join",
            new { minecraftUuid = user.MinecraftUuid });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Join_InvalidApiKey_Returns401()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "bad-key-that-does-not-exist");

        var response = await client.PostAsJsonAsync("/api/player/join",
            new { minecraftUuid = Guid.NewGuid().ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Join_MissingApiKeyHeader_Returns401()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/player/join",
            new { minecraftUuid = Guid.NewGuid().ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Join_UnknownMinecraftUuid_Returns404()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", server.ApiKey);

        var response = await client.PostAsJsonAsync("/api/player/join",
            new { minecraftUuid = "uuid-that-does-not-exist" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Join_NoExistingSession_CreatesNewSession()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);
        var uuid = Guid.NewGuid().ToString();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, minecraftUuid: uuid);

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", server.ApiKey);
        await client.PostAsJsonAsync("/api/player/join", new { minecraftUuid = uuid });

        var sessions = await db.PlayerSessions
            .Where(s => s.UserId == user.Id && s.ServerId == server.Id)
            .ToListAsync();
        sessions.Should().ContainSingle(s => s.LeftAt == null);
    }

    [Fact]
    public async Task Join_OrphanedOpenSessionExists_ClosesOrphanAndCreatesNew()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);
        var uuid = Guid.NewGuid().ToString();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, minecraftUuid: uuid);

        // Seed an orphaned open session
        var orphan = await TestDbHelper.CreateOpenSessionAsync(db, user.Id, server.Id,
            joinedAt: DateTime.UtcNow.AddHours(-1));

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", server.ApiKey);
        await client.PostAsJsonAsync("/api/player/join", new { minecraftUuid = uuid });

        await db.Entry(orphan).ReloadAsync();
        orphan.LeftAt.Should().NotBeNull("orphaned session should have been closed");

        var openCount = await db.PlayerSessions
            .CountAsync(s => s.UserId == user.Id && s.ServerId == server.Id && s.LeftAt == null);
        openCount.Should().Be(1, "only the new session should remain open");
    }

    [Fact]
    public async Task Leave_ValidApiKeyAndKnownUuid_Returns200()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);
        var uuid = Guid.NewGuid().ToString();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, minecraftUuid: uuid);
        await TestDbHelper.CreateOpenSessionAsync(db, user.Id, server.Id);

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", server.ApiKey);
        var response = await client.PostAsJsonAsync("/api/player/leave",
            new { minecraftUuid = uuid });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Leave_InvalidApiKey_Returns401()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "invalid-key");

        var response = await client.PostAsJsonAsync("/api/player/leave",
            new { minecraftUuid = Guid.NewGuid().ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Leave_UnknownMinecraftUuid_Returns404()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", server.ApiKey);
        var response = await client.PostAsJsonAsync("/api/player/leave",
            new { minecraftUuid = "not-a-real-uuid" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Leave_ClosesOpenSession_SetsLeftAtAndDurationSeconds()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);
        var uuid = Guid.NewGuid().ToString();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, minecraftUuid: uuid);
        var session = await TestDbHelper.CreateOpenSessionAsync(db, user.Id, server.Id);

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", server.ApiKey);
        await client.PostAsJsonAsync("/api/player/leave", new { minecraftUuid = uuid });

        await db.Entry(session).ReloadAsync();
        session.LeftAt.Should().NotBeNull();
        session.DurationSeconds.Should().NotBeNull().And.BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Leave_DurationSeconds_IsApproximatelyCorrect()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);
        var uuid = Guid.NewGuid().ToString();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, minecraftUuid: uuid);

        // Seed a session started 5 minutes ago
        var joinedAt = DateTime.UtcNow.AddMinutes(-5);
        var session = await TestDbHelper.CreateOpenSessionAsync(db, user.Id, server.Id, joinedAt);

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", server.ApiKey);
        await client.PostAsJsonAsync("/api/player/leave", new { minecraftUuid = uuid });

        await db.Entry(session).ReloadAsync();
        // Should be ~300 seconds; allow 10-second tolerance for test execution time
        session.DurationSeconds.Should().NotBeNull();
        session.DurationSeconds!.Value.Should().BeInRange(290, 310);
    }

    [Fact]
    public async Task Leave_NoOpenSession_Returns404()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);
        var uuid = Guid.NewGuid().ToString();
        await TestDbHelper.CreateUserAsync(scope.ServiceProvider, minecraftUuid: uuid);
        // No session seeded

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", server.ApiKey);
        var response = await client.PostAsJsonAsync("/api/player/leave",
            new { minecraftUuid = uuid });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
