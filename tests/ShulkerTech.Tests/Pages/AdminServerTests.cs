using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class AdminServerTests(ShulkerTechWebApplicationFactory factory)
{
    private const string AddUrl    = "/Admin/Servers?handler=Add";
    private const string EditHostUrl = "/Admin/Servers?handler=EditHost";

    private HttpClient AdminClient(string userId)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private static FormUrlEncodedContent AddForm(
        string name   = "Test SMP",
        string host   = "play.example.com",
        string port   = "25565",
        string? desc  = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["Input.Name"] = name,
            ["Input.Host"] = host,
            ["Input.Port"] = port,
        };
        if (desc is not null) fields["Input.Description"] = desc;
        return new FormUrlEncodedContent(fields);
    }

    // ── Add — happy path ──────────────────────────────────────────────────────

    [Fact]
    public async Task Add_ValidInput_SavesServerAndRedirects()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var name = $"Server {Guid.NewGuid():N}";
        var resp = await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: name, host: "mc.example.com", port: "25565"));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var server = await db.MinecraftServers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name);
        server.Should().NotBeNull();
        server!.Host.Should().Be("mc.example.com");
        server.Port.Should().Be(25565);
        server.IsActive.Should().BeTrue();
        server.ApiKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Add_WithDescription_SavesDescription()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var name = $"Desc Server {Guid.NewGuid():N}";
        await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: name, host: "mc.example.com", desc: "A great server"));

        var server = await db.MinecraftServers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name);
        server!.Description.Should().Be("A great server");
    }

    [Fact]
    public async Task Add_WhitespaceAroundHost_IsTrimmed()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var name = $"Trimmed {Guid.NewGuid():N}";
        await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: name, host: "  mc.example.com  "));

        var server = await db.MinecraftServers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name);
        server!.Host.Should().Be("mc.example.com");
    }

    [Fact]
    public async Task Add_NonDefaultPort_SavesCorrectly()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var name = $"Port Server {Guid.NewGuid():N}";
        await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: name, host: "mc.example.com", port: "19132"));

        var server = await db.MinecraftServers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name);
        server!.Port.Should().Be(19132);
    }

    // ── Add — validation failures ─────────────────────────────────────────────

    [Fact]
    public async Task Add_MissingName_ReturnsPageWithoutSaving()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var countBefore = await db.MinecraftServers.CountAsync();

        var resp = await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: "", host: "mc.example.com"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "validation failure should re-render the page");
        (await db.MinecraftServers.CountAsync()).Should().Be(countBefore);
    }

    [Fact]
    public async Task Add_MissingHost_ReturnsPageWithoutSaving()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var countBefore = await db.MinecraftServers.CountAsync();

        var resp = await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: $"No Host {Guid.NewGuid():N}", host: ""));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "host is now required");
        (await db.MinecraftServers.CountAsync()).Should().Be(countBefore);
    }

    [Fact]
    public async Task Add_WhitespaceOnlyHost_ReturnsPageWithoutSaving()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var countBefore = await db.MinecraftServers.CountAsync();

        var resp = await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: $"Whitespace Host {Guid.NewGuid():N}", host: "   "));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await db.MinecraftServers.CountAsync()).Should().Be(countBefore);
    }

    [Fact]
    public async Task Add_NegativePort_ReturnsPageWithoutSaving()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var countBefore = await db.MinecraftServers.CountAsync();

        var resp = await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: $"Bad Port {Guid.NewGuid():N}", host: "mc.example.com", port: "-1"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "[Range(1,65535)] should reject negative ports");
        (await db.MinecraftServers.CountAsync()).Should().Be(countBefore);
    }

    [Fact]
    public async Task Add_ZeroPort_ReturnsPageWithoutSaving()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var countBefore = await db.MinecraftServers.CountAsync();

        var resp = await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: $"Zero Port {Guid.NewGuid():N}", host: "mc.example.com", port: "0"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "port 0 is below the valid range");
        (await db.MinecraftServers.CountAsync()).Should().Be(countBefore);
    }

    [Fact]
    public async Task Add_PortTooHigh_ReturnsPageWithoutSaving()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var countBefore = await db.MinecraftServers.CountAsync();

        var resp = await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: $"High Port {Guid.NewGuid():N}", host: "mc.example.com", port: "65536"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "port 65536 exceeds the valid range");
        (await db.MinecraftServers.CountAsync()).Should().Be(countBefore);
    }

    [Fact]
    public async Task Add_NonNumericPort_ReturnsPageWithoutSaving()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var countBefore = await db.MinecraftServers.CountAsync();

        var resp = await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: $"Bad Port Type {Guid.NewGuid():N}", host: "mc.example.com", port: "notaport"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "non-numeric port should fail model binding");
        (await db.MinecraftServers.CountAsync()).Should().Be(countBefore);
    }

    // ── Add — port boundary values ────────────────────────────────────────────

    [Fact]
    public async Task Add_PortAtMinBoundary_Saves()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var name = $"Min Port {Guid.NewGuid():N}";
        var resp = await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: name, host: "mc.example.com", port: "1"));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var server = await db.MinecraftServers.AsNoTracking().FirstOrDefaultAsync(s => s.Name == name);
        server!.Port.Should().Be(1);
    }

    [Fact]
    public async Task Add_PortAtMaxBoundary_Saves()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var name = $"Max Port {Guid.NewGuid():N}";
        var resp = await AdminClient(admin.Id).PostAsync(AddUrl,
            AddForm(name: name, host: "mc.example.com", port: "65535"));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var server = await db.MinecraftServers.AsNoTracking().FirstOrDefaultAsync(s => s.Name == name);
        server!.Port.Should().Be(65535);
    }

    // ── Edit host ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task EditHost_ValidInput_UpdatesHostAndPort()
    {
        using var scope = factory.Services.CreateScope();
        var admin  = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"]            = server.Id.ToString(),
            ["EditHost.Host"] = "new.example.com",
            ["EditHost.Port"] = "19132",
        });

        var resp = await AdminClient(admin.Id).PostAsync(EditHostUrl, form);
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var verify = factory.Services.CreateScope();
        var updated = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .MinecraftServers.AsNoTracking().FirstAsync(s => s.Id == server.Id);
        updated.Host.Should().Be("new.example.com");
        updated.Port.Should().Be(19132);
    }

    [Fact]
    public async Task EditHost_EmptyHost_ReturnsPageWithoutSaving()
    {
        using var scope = factory.Services.CreateScope();
        var admin  = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);
        var originalHost = server.Host;

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"]            = server.Id.ToString(),
            ["EditHost.Host"] = "",
            ["EditHost.Port"] = "25565",
        });

        var resp = await AdminClient(admin.Id).PostAsync(EditHostUrl, form);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "empty host should fail validation");

        using var verify = factory.Services.CreateScope();
        var unchanged = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .MinecraftServers.AsNoTracking().FirstAsync(s => s.Id == server.Id);
        unchanged.Host.Should().Be(originalHost, "host must not be cleared");
    }

    [Fact]
    public async Task EditHost_NegativePort_ReturnsPageWithoutSaving()
    {
        using var scope = factory.Services.CreateScope();
        var admin  = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);
        var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var server = await TestDbHelper.CreateServerAsync(db);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"]            = server.Id.ToString(),
            ["EditHost.Host"] = "mc.example.com",
            ["EditHost.Port"] = "-1",
        });

        var resp = await AdminClient(admin.Id).PostAsync(EditHostUrl, form);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "negative port should fail [Range] validation");

        using var verify = factory.Services.CreateScope();
        var unchanged = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .MinecraftServers.AsNoTracking().FirstAsync(s => s.Id == server.Id);
        unchanged.Port.Should().Be(server.Port, "port must not be changed on validation failure");
    }

    // ── Access control ────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_AsNonAdmin_IsRedirectedAway()
    {
        using var scope = factory.Services.CreateScope();
        var member = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        var resp = await AdminClient(member.Id).PostAsync(AddUrl,
            AddForm(name: "Blocked", host: "mc.example.com"));

        resp.StatusCode.Should().BeOneOf(
            [HttpStatusCode.Redirect, HttpStatusCode.Forbidden],
            "non-admins must not be able to add servers");
        resp.Headers.Location?.ToString().Should().NotContain("/Admin/");
    }

    [Fact]
    public async Task Add_Unauthenticated_IsRedirectedToLogin()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var resp = await client.PostAsync(AddUrl, AddForm(name: "Anon", host: "mc.example.com"));

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location?.ToString().Should().Contain("Login");
    }
}
