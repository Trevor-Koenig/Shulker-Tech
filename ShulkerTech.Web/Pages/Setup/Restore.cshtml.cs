using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Pages.Setup;

[RequestSizeLimit(512L * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 512L * 1024 * 1024)]
public class RestoreModel(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ApplicationDbContext db,
    RoleManager<IdentityRole> roleManager) : PageModel
{
    public string? ErrorMessage { get; set; }
    public List<BackupInfo> AvailableBackups { get; set; } = [];

    public record BackupInfo(string FileName, DateTime Timestamp, long SizeBytes)
    {
        public string DisplayName =>
            $"{Timestamp:MMM d, yyyy  HH:mm} UTC  ({SizeBytes / 1024.0:F0} KB)";
    }

    public IActionResult OnGet()
    {
        if (userManager.Users.Any())
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        AvailableBackups = ScanBackups();
        return Page();
    }

    // Restore from a file already on disk in BackupDir.
    public async Task<IActionResult> OnPostExistingAsync(string setupCode, string backupFile)
    {
        AvailableBackups = ScanBackups();

        if (!ValidateSetupCode(setupCode))
            return Page();

        var safeFileName = Path.GetFileName(backupFile);
        var backupDir    = configuration["BackupDir"] ?? "/backups";
        var backupPath   = Path.GetFullPath(Path.Combine(backupDir, safeFileName));

        if (!backupPath.StartsWith(Path.GetFullPath(backupDir) + Path.DirectorySeparatorChar) ||
            !System.IO.File.Exists(backupPath) ||
            !safeFileName.EndsWith(".sql.gz", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Backup file not found or invalid.";
            return Page();
        }

        return await RunRestoreAsync(backupPath, tempFile: false);
    }

    // Restore from a user-uploaded .sql.gz file.
    public async Task<IActionResult> OnPostUploadAsync(string setupCode, IFormFile? uploadedBackup)
    {
        AvailableBackups = ScanBackups();

        if (!ValidateSetupCode(setupCode))
            return Page();

        if (uploadedBackup is null || uploadedBackup.Length == 0)
        {
            ErrorMessage = "No file was uploaded.";
            return Page();
        }

        var originalName = Path.GetFileName(uploadedBackup.FileName);
        if (!originalName.EndsWith(".sql.gz", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Only .sql.gz backup files are accepted.";
            return Page();
        }

        // Write the upload to a temp path so RestoreBackupAsync can stream it via psql.
        var tempPath = Path.Combine(Path.GetTempPath(), $"shulker-restore-{Guid.NewGuid():N}.sql.gz");
        try
        {
            await using (var fs = System.IO.File.Create(tempPath))
                await uploadedBackup.CopyToAsync(fs);

            return await RunRestoreAsync(tempPath, tempFile: true);
        }
        catch
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
            throw;
        }
    }

    private bool ValidateSetupCode(string setupCode)
    {
        var expected = configuration["SETUP_CODE"];
        if (string.IsNullOrEmpty(expected) || setupCode != expected)
        {
            ErrorMessage = "Invalid setup code.";
            return false;
        }
        return true;
    }

    private async Task<IActionResult> RunRestoreAsync(string backupPath, bool tempFile)
    {
        try
        {
            await RestoreBackupAsync(backupPath);

            await db.Database.MigrateAsync();
            foreach (var role in new[] { "Admin", "Moderator", "Member" })
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));

            if (!await db.SitePermissions.AnyAsync(p => p.RoleName == "Admin"))
            {
                db.SitePermissions.AddRange(
                    SiteResource.All
                        .Where(r => !r.IsPublicByDefault)
                        .Select(r => new SitePermission { RoleName = "Admin", Resource = r.Key }));
                await db.SaveChangesAsync();
            }

            if ((await userManager.GetUsersInRoleAsync("Admin")).Count == 0)
            {
                var candidates = await userManager.Users.Take(1).ToListAsync();
                foreach (var candidate in candidates)
                    await userManager.AddToRoleAsync(candidate, "Admin");
            }

            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Restore failed: {ex.Message}";
            return Page();
        }
        finally
        {
            if (tempFile && System.IO.File.Exists(backupPath))
                System.IO.File.Delete(backupPath);
        }
    }

    internal List<BackupInfo> ScanBackups()
    {
        var dir = configuration["BackupDir"] ?? "/backups";
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir, "backup_*.sql.gz")
            .Select(f =>
            {
                var fi   = new FileInfo(f);
                var stem = Path.GetFileNameWithoutExtension(
                               Path.GetFileNameWithoutExtension(fi.Name));
                DateTime ts = fi.LastWriteTimeUtc;
                if (stem.Length >= 22 &&
                    DateTime.TryParseExact(stem[7..], "yyyyMMdd_HHmmss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    ts = parsed;
                return new BackupInfo(fi.Name, ts, fi.Length);
            })
            .OrderByDescending(b => b.Timestamp)
            .ToList();
    }

    private async Task RestoreBackupAsync(string backupPath)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;
        var csb = new NpgsqlConnectionStringBuilder(connectionString);

        var host = csb.Host!;
        var port = (csb.Port == 0 ? 5432 : csb.Port).ToString();
        var user = csb.Username!;
        var database = csb.Database!;
        var pass = csb.Password ?? "";

        ProcessStartInfo MakePsi(string exe)
        {
            var p = new ProcessStartInfo
            {
                FileName = exe,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            p.ArgumentList.Add("-h"); p.ArgumentList.Add(host);
            p.ArgumentList.Add("-p"); p.ArgumentList.Add(port);
            p.ArgumentList.Add("-U"); p.ArgumentList.Add(user);
            p.ArgumentList.Add(database);
            p.Environment["PGPASSWORD"] = pass;
            return p;
        }

        var cleanPsi = MakePsi("psql");
        cleanPsi.ArgumentList.Add("-c");
        cleanPsi.ArgumentList.Add($"DROP SCHEMA public CASCADE; CREATE SCHEMA public; GRANT ALL ON SCHEMA public TO \"{user}\"; GRANT ALL ON SCHEMA public TO public;");
        using (var cleanProc = Process.Start(cleanPsi)!)
        {
            cleanProc.StandardInput.Close();
            var cleanErr = await cleanProc.StandardError.ReadToEndAsync();
            await cleanProc.WaitForExitAsync();
            if (cleanProc.ExitCode != 0)
                throw new InvalidOperationException($"Schema wipe failed: {cleanErr.Trim()}");
        }

        var restorePsi = MakePsi("psql");
        restorePsi.ArgumentList.Add("--set=ON_ERROR_STOP=1");
        using var process = Process.Start(restorePsi)!;

        await using var file = System.IO.File.OpenRead(backupPath);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        await gzip.CopyToAsync(process.StandardInput.BaseStream);
        process.StandardInput.Close();

        var stderr = await process.StandardError.ReadToEndAsync();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        await process.WaitForExitAsync(timeoutCts.Token);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(stderr.Trim());
    }
}
