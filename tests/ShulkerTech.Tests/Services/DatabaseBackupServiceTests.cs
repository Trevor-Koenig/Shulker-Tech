using System.Diagnostics;
using System.IO.Compression;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShulkerTech.Tests.Infrastructure;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Tests.Services;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class DatabaseBackupServiceTests(ShulkerTechWebApplicationFactory factory)
{
    [Fact]
    public async Task Backup_CreatesValidGzippedSqlDump()
    {
        if (!IsPgDumpAvailable())
            return; // pg_dump not on PATH in this environment — skip

        var dir = Path.Combine(Path.GetTempPath(), $"shulker-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var service = BuildService(dir);

            using var cts = new CancellationTokenSource();
            await service.StartAsync(cts.Token);

            // The startup backup fires immediately inside ExecuteAsync.
            // Poll until the file appears and its size is stable (fully flushed).
            var backupFile = await WaitForBackupFileAsync(dir, TimeSpan.FromSeconds(30));

            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            backupFile.Should().NotBeNull("pg_dump should produce a backup file within 30 s");
            backupFile!.Refresh();
            backupFile.Length.Should().BeGreaterThan(0,
                "backup file must not be empty — the GZipStream must be flushed before the size is read");

            // Decompress and verify it's a genuine PostgreSQL dump
            await using var fs = File.OpenRead(backupFile.FullName);
            await using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var reader = new StreamReader(gz);
            var content = await reader.ReadToEndAsync();

            content.Should().Contain("PostgreSQL database dump",
                "decompressed content should be a pg_dump SQL file");
            content.Should().Contain("CREATE TABLE",
                "dump should include the application schema");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Backup_PrunesFilesOlderThanRetentionPeriod()
    {
        if (!IsPgDumpAvailable())
            return;

        var dir = Path.Combine(Path.GetTempPath(), $"shulker-backup-prune-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            // Plant a stale backup (15 days old — beyond the 14-day retention window)
            var staleFile = Path.Combine(dir, "backup_20000101_000000.sql.gz");
            await File.WriteAllBytesAsync(staleFile, []);
            File.SetLastWriteTimeUtc(staleFile, DateTime.UtcNow.AddDays(-15));

            var service = BuildService(dir);

            using var cts = new CancellationTokenSource();
            await service.StartAsync(cts.Token);

            // Wait for the fresh backup to appear (pruning runs after each backup)
            await WaitForBackupFileAsync(dir, TimeSpan.FromSeconds(30),
                excludeName: Path.GetFileName(staleFile));

            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            File.Exists(staleFile).Should().BeFalse(
                "files older than the retention window should be pruned after each backup");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private DatabaseBackupService BuildService(string backupDir)
    {
        using var scope = factory.Services.CreateScope();
        var realConfig = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connStr = realConfig.GetConnectionString("DefaultConnection")!;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connStr,
                ["BackupDir"] = backupDir,
            })
            .Build();

        return new DatabaseBackupService(config, NullLogger<DatabaseBackupService>.Instance);
    }

    // Poll until a backup_*.sql.gz file appears and its size is stable across
    // two consecutive reads, meaning the GZipStream has been fully flushed.
    private static async Task<FileInfo?> WaitForBackupFileAsync(
        string dir,
        TimeSpan timeout,
        string? excludeName = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        long previousSize = -1;

        while (DateTime.UtcNow < deadline)
        {
            var file = Directory.GetFiles(dir, "backup_*.sql.gz")
                .Where(f => excludeName == null || Path.GetFileName(f) != excludeName)
                .Select(f => new FileInfo(f))
                .FirstOrDefault();

            if (file != null)
            {
                file.Refresh();
                if (file.Length > 0 && file.Length == previousSize)
                    return file;
                previousSize = file.Length;
            }

            await Task.Delay(300);
        }

        return null;
    }

    private static bool IsPgDumpAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pg_dump",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("--version");
            using var p = Process.Start(psi)!;
            p.WaitForExit(3000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }
}
