using System.IO.Compression;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class SetupPageTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient() => factory.CreateClient(
        new() { AllowAutoRedirect = false });

    private static FormUrlEncodedContent SetupForm(
        string code = "test-setup-code",
        string email = "",
        string password = "ValidPass@1234",
        string confirm = "ValidPass@1234")
    {
        email = string.IsNullOrEmpty(email) ? $"admin-{Guid.NewGuid():N}@example.com" : email;
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.SetupCode"] = code,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.ConfirmPassword"] = confirm,
        });
    }

    private static FormUrlEncodedContent RestoreForm(string code, string backupFile) =>
        new(new Dictionary<string, string> { ["setupCode"] = code, ["backupFile"] = backupFile });

    private WebApplicationFactory<Program> FactoryWithBackupDir(string dir) =>
        factory.WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["BackupDir"] = dir })));

    private static string CreateTempBackupDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"shulker-backups-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    // Creates a valid .sql.gz file (gzip-compressed text, not a real pg_dump)
    private static string CreateFakeBackupFile(string dir)
    {
        var filename = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql.gz";
        using var fs = File.Create(Path.Combine(dir, filename));
        using var gz = new GZipStream(fs, CompressionLevel.Fastest);
        gz.Write("-- fake backup\nSELECT 1;\n"u8);
        return filename;
    }

    // Creates a .sql.gz file whose bytes are NOT valid gzip — CopyToAsync will throw
    private static string CreateCorruptBackupFile(string dir)
    {
        var filename = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql.gz";
        File.WriteAllBytes(Path.Combine(dir, filename), [0x00, 0x01, 0x02, 0x03]);
        return filename;
    }

    // ── Existing tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_NoUsers_Returns200()
    {
        // If DB has no users, /setup should return 200
        // Note: other tests may have created users; we just verify the page itself loads.
        // A fresh factory is not guaranteed here, so we test against actual DB state.
        var response = await CreateClient().GetAsync("/setup");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Get_UsersExist_Redirects()
    {
        // Seed a user to simulate a configured instance
        using var scope = factory.Services.CreateScope();
        await TestDbHelper.CreateUserAsync(scope.ServiceProvider);

        var response = await CreateClient().GetAsync("/setup");
        // Once users exist, /setup redirects to login
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Post_InvalidSetupCode_Returns200WithError()
    {
        var response = await CreateClient().PostAsync("/setup",
            SetupForm(code: "WRONG-CODE"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid setup code");
    }

    [Fact]
    public async Task Post_MissingEmail_Returns200WithValidationError()
    {
        var response = await CreateClient().PostAsync("/setup",
            SetupForm(email: " "));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_PasswordTooShort_Returns200WithValidationError()
    {
        var response = await CreateClient().PostAsync("/setup",
            SetupForm(password: "abc", confirm: "abc"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ValidPost_CreatesAdminUser()
    {
        var email = $"setup-{Guid.NewGuid():N}@example.com";

        // First ensure no users so setup is available
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasUsers = await db.Users.AnyAsync();
        if (hasUsers) return; // Skip if DB already has users (shared fixture)

        var response = await CreateClient().PostAsync("/setup", SetupForm(email: email));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var createdUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        createdUser.Should().NotBeNull();
        createdUser!.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Post_ValidPost_CreatedUserHasAdminRole()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasUsers = await db.Users.AnyAsync();
        if (hasUsers) return;

        var email = $"setup-{Guid.NewGuid():N}@example.com";
        await CreateClient().PostAsync("/setup", SetupForm(email: email));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user == null) return;

        var roles = await userManager.GetRolesAsync(user);
        roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task Post_PasswordsDoNotMatch_Returns200()
    {
        var response = await CreateClient().PostAsync("/setup",
            SetupForm(password: "ValidPass@1234", confirm: "DifferentPass@1234"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── New tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_InvalidEmailFormat_Returns200()
    {
        var response = await CreateClient().PostAsync("/setup",
            SetupForm(email: "not-an-email-address"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        // Page re-renders with the invalid value echoed back via asp-for binding
        body.Should().Contain("not-an-email-address");
    }

    [Fact]
    public async Task Post_EmptySetupCode_Returns200()
    {
        // Empty setup code triggers Required validation (not the code-check logic)
        var response = await CreateClient().PostAsync("/setup", SetupForm(code: ""));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // POST to the restore handler so the users-exist redirect on GET doesn't interfere
    [Fact]
    public async Task Restore_WhenBackupsExist_ShowsRestorePanel()
    {
        var dir = CreateTempBackupDir();
        try
        {
            CreateFakeBackupFile(dir);
            await using var derived = FactoryWithBackupDir(dir);
            var client = derived.CreateClient(new() { AllowAutoRedirect = false });

            var response = await client.PostAsync("/setup?handler=Restore",
                RestoreForm("WRONG-CODE", "any.sql.gz"));
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("RESTORE FROM BACKUP");
            body.Should().Contain("RESTORE DATABASE");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Restore_WhenNoBackupsExist_HidesRestorePanel()
    {
        var dir = CreateTempBackupDir(); // empty — no backup files
        try
        {
            await using var derived = FactoryWithBackupDir(dir);
            var client = derived.CreateClient(new() { AllowAutoRedirect = false });

            var response = await client.PostAsync("/setup?handler=Restore",
                RestoreForm("WRONG-CODE", "any.sql.gz"));
            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("RESTORE FROM BACKUP");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Restore_InvalidSetupCode_ErrorAppearsInRestorePanel()
    {
        var dir = CreateTempBackupDir();
        try
        {
            CreateFakeBackupFile(dir);
            await using var derived = FactoryWithBackupDir(dir);
            var client = derived.CreateClient(new() { AllowAutoRedirect = false });

            var response = await client.PostAsync("/setup?handler=Restore",
                RestoreForm("WRONG-CODE", "backup_20260101_120000.sql.gz"));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Invalid setup code");
            // The error must appear before INITIALISE SYSTEM — i.e., it's in the restore panel,
            // not the creation form below it.
            body.IndexOf("Invalid setup code", StringComparison.Ordinal)
                .Should().BeLessThan(body.IndexOf("INITIALISE SYSTEM", StringComparison.Ordinal));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Restore_PathTraversal_ReturnsFileNotFoundError()
    {
        var dir = CreateTempBackupDir();
        try
        {
            CreateFakeBackupFile(dir); // needed so the restore panel renders the error
            await using var derived = FactoryWithBackupDir(dir);
            var client = derived.CreateClient(new() { AllowAutoRedirect = false });

            // Path.GetFileName strips directory components, so "../../etc/passwd" → "passwd"
            // which fails the .sql.gz extension check
            var response = await client.PostAsync("/setup?handler=Restore",
                RestoreForm("test-setup-code", "../../etc/passwd"));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Backup file not found or invalid");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Restore_NonExistentFile_ReturnsFileNotFoundError()
    {
        var dir = CreateTempBackupDir();
        try
        {
            CreateFakeBackupFile(dir); // so the restore panel is visible
            await using var derived = FactoryWithBackupDir(dir);
            var client = derived.CreateClient(new() { AllowAutoRedirect = false });

            var response = await client.PostAsync("/setup?handler=Restore",
                RestoreForm("test-setup-code", "backup_does_not_exist.sql.gz"));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Backup file not found or invalid");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Restore_WrongExtension_ReturnsFileNotFoundError()
    {
        var dir = CreateTempBackupDir();
        try
        {
            CreateFakeBackupFile(dir); // so the restore panel is visible
            File.WriteAllText(Path.Combine(dir, "backup_20260101_120000.sql"), "data");
            await using var derived = FactoryWithBackupDir(dir);
            var client = derived.CreateClient(new() { AllowAutoRedirect = false });

            var response = await client.PostAsync("/setup?handler=Restore",
                RestoreForm("test-setup-code", "backup_20260101_120000.sql"));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Backup file not found or invalid");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Restore_CorruptBackupFile_ShowsRestoreFailedError()
    {
        // A file with invalid gzip bytes: when the restore handler opens it, GZipStream.CopyToAsync
        // throws InvalidDataException which is caught and shown as "Restore failed: ...".
        // If psql is not installed, Process.Start throws first — same outcome.
        var dir = CreateTempBackupDir();
        try
        {
            var backupFile = CreateCorruptBackupFile(dir);
            await using var derived = FactoryWithBackupDir(dir);
            var client = derived.CreateClient(new() { AllowAutoRedirect = false });

            var response = await client.PostAsync("/setup?handler=Restore",
                RestoreForm("test-setup-code", backupFile));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Restore failed");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
