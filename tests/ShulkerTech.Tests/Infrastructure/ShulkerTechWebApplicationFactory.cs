using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Core.Services;
using ShulkerTech.Web.Services;
using Testcontainers.PostgreSql;

namespace ShulkerTech.Tests.Infrastructure;

public class ShulkerTechWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:latest")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["SETUP_CODE"] = "test-setup-code",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove background services that perform I/O
            RemoveHostedService<ServerStatusRefresher>(services);
            RemoveHostedService<DatabaseBackupService>(services);

            // Replace MinecraftPingService with a mock that always returns Offline
            var pingDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(MinecraftPingService));
            if (pingDescriptor != null) services.Remove(pingDescriptor);
            var mockPing = new Mock<MinecraftPingService>();
            mockPing.Setup(m => m.PingAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServerPingResult.Offline);
            services.AddSingleton(mockPing.Object);

            // Add test auth scheme and make it the default so tests can pass
            // X-Test-User-Id to authenticate without a real login flow.
            // Falls back to anonymous when the header is absent.
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuth", _ => { });

            services.PostConfigure<AuthenticationOptions>(opts =>
            {
                opts.DefaultAuthenticateScheme = "TestAuth";
                // Keep Identity as the challenge scheme so [Authorize] redirects to login
                opts.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Run migrations and seed roles now that the container is up
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Moderator", "Member" })
        {
            if (!roleManager.RoleExistsAsync(role).GetAwaiter().GetResult())
                roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
        }

        return host;
    }

    private static void RemoveHostedService<T>(IServiceCollection services) where T : class
    {
        var descriptor = services.FirstOrDefault(d => d.ImplementationType == typeof(T));
        if (descriptor != null) services.Remove(descriptor);
    }
}
