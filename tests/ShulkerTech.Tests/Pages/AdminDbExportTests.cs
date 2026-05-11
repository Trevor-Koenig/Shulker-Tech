using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShulkerTech.Tests.Infrastructure;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class AdminDbExportTests(ShulkerTechWebApplicationFactory factory)
{
    private const string PageUrl   = "/Admin/Site/DbExport";
    private const string ExportUrl = "/Admin/Site/DbExport?handler=Export";

    private HttpClient AdminClient(string userId)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    // ── Access control ────────────────────────────────────────────────────────

    [Fact]
    public async Task Page_Unauthenticated_RedirectsToLogin()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var resp = await client.GetAsync(PageUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location?.ToString().Should().Contain("Login");
    }

    [Fact]
    public async Task Export_Unauthenticated_RedirectsToLogin()
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var resp = await client.GetAsync(ExportUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location?.ToString().Should().Contain("Login");
    }

    [Fact]
    public async Task Page_NonAdmin_IsRejected()
    {
        using var scope = factory.Services.CreateScope();
        var member = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        var resp = await AdminClient(member.Id).GetAsync(PageUrl);

        resp.StatusCode.Should().BeOneOf(
            [HttpStatusCode.Redirect, HttpStatusCode.Forbidden],
            "non-admin must not access the export page");
    }

    [Fact]
    public async Task Export_NonAdmin_IsRejected()
    {
        using var scope = factory.Services.CreateScope();
        var member = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");

        var resp = await AdminClient(member.Id).GetAsync(ExportUrl);

        resp.StatusCode.Should().BeOneOf(
            [HttpStatusCode.Redirect, HttpStatusCode.Forbidden],
            "non-admin must not trigger a database export");
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Page_Admin_Returns200()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);

        var resp = await AdminClient(admin.Id).GetAsync(PageUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Export_Admin_ReturnsGzipFile()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);

        var resp = await AdminClient(admin.Id).GetAsync(ExportUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/gzip");
        resp.Content.Headers.ContentDisposition?.FileName.Should()
            .MatchRegex(@"shulkertech-export-\d{8}_\d{6}\.sql\.gz");
    }

    [Fact]
    public async Task Export_Admin_ResponseBodyIsNonEmpty()
    {
        using var scope = factory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);

        var resp = await AdminClient(admin.Id).GetAsync(ExportUrl);
        var body = await resp.Content.ReadAsByteArrayAsync();

        body.Should().NotBeEmpty("the mock exporter writes a payload");
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_WhenExporterThrows_RedirectsBackToPage()
    {
        // Build a one-off client backed by a factory that registers a failing exporter.
        await using var failFactory = new FailingExporterFactory();
        await failFactory.InitializeAsync();

        using var scope = failFactory.Services.CreateScope();
        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true);

        var client = failFactory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User-Id", admin.Id);

        var resp = await client.GetAsync(ExportUrl);

        // Handler catches the exception and redirects back to the page
        resp.StatusCode.Should().Be(HttpStatusCode.Redirect);
        resp.Headers.Location?.ToString().Should().Contain("DbExport");
    }
}

/// <summary>Variant factory that swaps in an exporter that always throws.</summary>
internal sealed class FailingExporterFactory : ShulkerTechWebApplicationFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDatabaseExporter));
            if (descriptor != null) services.Remove(descriptor);

            var mock = new Mock<IDatabaseExporter>();
            mock.Setup(e => e.ExportAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("pg_dump not found"));
            services.AddScoped<IDatabaseExporter>(_ => mock.Object);
        });
    }
}
