using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Pages.Setup;

public class IndexModel(
    UserManager<ApplicationUser> userManager,
    IUserStore<ApplicationUser> userStore,
    SignInManager<ApplicationUser> signInManager,
    IConfiguration configuration) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? RestoreErrorMessage { get; set; }
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

    public async Task<IActionResult> OnPostAsync()
    {
        AvailableBackups = ScanBackups();

        if (!ModelState.IsValid)
            return Page();

        var expectedCode = configuration["SETUP_CODE"];
        if (string.IsNullOrEmpty(expectedCode) || Input.SetupCode != expectedCode)
        {
            ErrorMessage = "Invalid setup code.";
            return Page();
        }

        var user = new ApplicationUser { IsAdmin = true };
        var emailStore = (IUserEmailStore<ApplicationUser>)userStore;
        await userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
        await emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        await userManager.AddToRoleAsync(user, "Admin");

        var confirmToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await userManager.ConfirmEmailAsync(user, confirmToken);

        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostRestoreAsync(string setupCode, string backupFile)
    {
        ModelState.Clear(); // Input.* fields are not part of the restore form
        AvailableBackups = ScanBackups();

        var expectedCode = configuration["SETUP_CODE"];
        if (string.IsNullOrEmpty(expectedCode) || setupCode != expectedCode)
        {
            RestoreErrorMessage = "Invalid setup code.";
            return Page();
        }

        // Prevent path traversal — only allow bare filenames inside the backup dir
        var safeFileName = Path.GetFileName(backupFile);
        var backupDir = configuration["BackupDir"] ?? "/backups";
        var backupPath = Path.GetFullPath(Path.Combine(backupDir, safeFileName));

        if (!backupPath.StartsWith(Path.GetFullPath(backupDir) + Path.DirectorySeparatorChar) ||
            !System.IO.File.Exists(backupPath) ||
            !safeFileName.EndsWith(".sql.gz", StringComparison.OrdinalIgnoreCase))
        {
            RestoreErrorMessage = "Backup file not found or invalid.";
            return Page();
        }

        try
        {
            await RestoreBackupAsync(backupPath);
            // After restore users exist — send to login
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }
        catch (Exception ex)
        {
            RestoreErrorMessage = $"Restore failed: {ex.Message}";
            return Page();
        }
    }

    private List<BackupInfo> ScanBackups()
    {
        var dir = configuration["BackupDir"] ?? "/backups";
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir, "backup_*.sql.gz")
            .Select(f =>
            {
                var fi = new FileInfo(f);
                // Parse timestamp embedded in filename: backup_YYYYMMDD_HHmmss.sql.gz
                var stem = Path.GetFileNameWithoutExtension(
                               Path.GetFileNameWithoutExtension(fi.Name)); // strip .gz, then .sql
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
        var db   = csb.Database!;
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
            p.ArgumentList.Add(db);
            p.Environment["PGPASSWORD"] = pass;
            return p;
        }

        // Wipe the public schema so the restore runs against a clean slate.
        // Without this, CREATE TABLE / ADD CONSTRAINT fail because the tables
        // already exist from EF migrations, and COPY hits live FK constraints
        // in the wrong insertion order.
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

        // Restore the backup, stopping on the first error so partial restores
        // are surfaced rather than silently producing a corrupt database.
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

    public class InputModel
    {
        [Required]
        public string SetupCode { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
