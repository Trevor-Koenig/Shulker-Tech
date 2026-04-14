using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using ShulkerTech.Core.Services;

namespace ShulkerTech.Tests.Infrastructure;

/// <summary>
/// Factory for registration/email tests. Extends the shared factory with:
/// - A trackable IEmailSender mock so tests can verify SendEmailAsync calls.
/// - A configurable MojangService mock so tests control Mojang API responses.
/// </summary>
public class RegisterEmailTestsFactory : ShulkerTechWebApplicationFactory
{
    public Mock<IEmailSender> EmailSenderMock { get; } = new();
    public Mock<IMojangService> MojangMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Replace the base factory's no-op email mock with a trackable one
            var emailDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailSender));
            if (emailDescriptor != null) services.Remove(emailDescriptor);
            services.AddSingleton<IEmailSender>(EmailSenderMock.Object);

            // Replace the real MojangService with a mock that tests can configure
            var mojangDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMojangService));
            if (mojangDescriptor != null) services.Remove(mojangDescriptor);
            services.AddSingleton<IMojangService>(MojangMock.Object);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Seed a bootstrap admin so FirstRunMiddleware doesn't intercept registration requests
        using var scope = host.Services.CreateScope();
        TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true, role: "Admin")
            .GetAwaiter().GetResult();

        return host;
    }

    public void SetupMojangReturnsProfile(string username = "TestPlayer", string? uuid = null)
    {
        uuid ??= Guid.NewGuid().ToString("N");
        MojangMock.Setup(m => m.GetProfileAsync(It.IsAny<string>()))
            .ReturnsAsync(new MojangProfile(uuid, username));
    }

    public void SetupMojangReturnsNull()
    {
        MojangMock.Setup(m => m.GetProfileAsync(It.IsAny<string>()))
            .ReturnsAsync((MojangProfile?)null);
    }
}
