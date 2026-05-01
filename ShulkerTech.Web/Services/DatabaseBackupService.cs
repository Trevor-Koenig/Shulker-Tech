using System.Diagnostics;
using System.IO.Compression;
using Npgsql;

namespace ShulkerTech.Web.Services;

public class DatabaseBackupService(
    IConfiguration configuration,
    ILogger<DatabaseBackupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private string BackupDir => configuration["BackupDir"] ?? "/backups";
    private const int RetentionDays = 14;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await RunBackupAsync("startup");

        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await RunBackupAsync("scheduled");
        }
        catch (OperationCanceledException) { }

        await RunBackupAsync("shutdown");
    }

    private async Task RunBackupAsync(string reason)
    {
        try
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")!;
            var csb = new NpgsqlConnectionStringBuilder(connectionString);

            Directory.CreateDirectory(BackupDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var path = Path.Combine(BackupDir, $"backup_{timestamp}.sql.gz");

            logger.LogInformation("Starting {Reason} backup → {Path}", reason, path);

            var psi = new ProcessStartInfo
            {
                FileName = "pg_dump",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("-h"); psi.ArgumentList.Add(csb.Host!);
            psi.ArgumentList.Add("-p"); psi.ArgumentList.Add((csb.Port == 0 ? 5432 : csb.Port).ToString());
            psi.ArgumentList.Add("-U"); psi.ArgumentList.Add(csb.Username!);
            psi.ArgumentList.Add(csb.Database!);
            psi.Environment["PGPASSWORD"] = csb.Password ?? "";

            using var process = Process.Start(psi)!;

            string stderrResult;
            {
                // Scope gzip/file so they are flushed and closed before we read the
                // file size — GZipStream buffers internally and the size would read 0
                // if we checked before disposal.
                await using var file = File.Create(path);
                await using var gzip = new GZipStream(file, CompressionLevel.Optimal);

                var copyTask = process.StandardOutput.BaseStream.CopyToAsync(gzip);
                var stderrTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(copyTask, stderrTask);
                stderrResult = stderrTask.Result;
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            await process.WaitForExitAsync(timeoutCts.Token);

            if (process.ExitCode != 0)
            {
                logger.LogError("pg_dump failed (exit {Code}): {Stderr}", process.ExitCode, stderrResult);
                File.Delete(path);
                return;
            }

            logger.LogInformation("Backup complete ({Size:N0} bytes)", new FileInfo(path).Length);

            foreach (var old in Directory.GetFiles(BackupDir, "backup_*.sql.gz")
                         .Select(f => new FileInfo(f))
                         .Where(f => f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-RetentionDays)))
            {
                old.Delete();
                logger.LogInformation("Pruned old backup: {File}", old.Name);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup failed ({Reason})", reason);
        }
    }
}
