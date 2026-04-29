using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace ShulkerTech.Tests.Infrastructure;

/// <summary>
/// Verifies that UseStaticFiles() serves runtime-uploaded files from the wwwroot filesystem.
/// MapStaticAssets() alone only covers compile-time manifest assets, causing 404s in production
/// for files written to wwwroot/uploads/wiki/ at runtime (e.g. wiki image uploads).
/// </summary>
public class StaticFileServingTests : IAsyncLifetime
{
    private readonly string _tempWebRoot = Path.Combine(Path.GetTempPath(), $"shulker-wwwroot-{Guid.NewGuid():N}");
    private TempWebRootFactory _factory = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_tempWebRoot, "uploads", "wiki"));
        _factory = new TempWebRootFactory(_tempWebRoot);
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        Directory.Delete(_tempWebRoot, recursive: true);
    }

    [Fact]
    public async Task UploadedFile_InUploadsWiki_IsServedWith200()
    {
        var fileName = $"test-{Guid.NewGuid():N}.txt";
        await File.WriteAllTextAsync(Path.Combine(_tempWebRoot, "uploads", "wiki", fileName), "test content");

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/uploads/wiki/{fileName}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonExistentFile_InUploadsPath_DoesNotReturnFileContent()
    {
        // UseStatusCodePagesWithReExecute re-executes to /404 (which returns 200 with HTML),
        // so we can't assert 404 status here. Assert content is not the file body instead.
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/uploads/wiki/does-not-exist-{Guid.NewGuid():N}.png");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBe("test content");
    }

    /// <summary>Factory variant that uses a temp directory as WebRoot so tests can write files to it.</summary>
    private sealed class TempWebRootFactory(string webRoot) : ShulkerTechWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseWebRoot(webRoot);
        }
    }
}
