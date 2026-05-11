using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace ShulkerTech.Web.Areas.Admin.Pages.Servers;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public List<ServerViewModel> Servers { get; set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Required(ErrorMessage = "Host is required — enter the server's hostname or IP address.")]
        public string Host { get; set; } = string.Empty;
        [Range(1, 65535)]
        public int Port { get; set; } = 25565;
    }

    public class EditHostModel
    {
        [Required(ErrorMessage = "Host is required.")]
        public string Host { get; set; } = string.Empty;
        [Range(1, 65535)]
        public int Port { get; set; } = 25565;
    }

    [BindProperty]
    public EditHostModel EditHost { get; set; } = new();

    public record ServerViewModel(
        int Id,
        string Name,
        string? Description,
        string? Host,
        int Port,
        bool IsActive,
        string ApiKey,
        int TotalSessions,
        long TotalPlaytimeSeconds,
        DateTime CreatedAt);

    public async Task OnGetAsync()
    {
        var servers = await db.MinecraftServers
            .OrderBy(s => s.Name)
            .ToListAsync();

        var sessionStats = await db.PlayerSessions
            .Where(ps => ps.DurationSeconds != null)
            .GroupBy(ps => ps.ServerId)
            .Select(g => new
            {
                ServerId = g.Key,
                TotalSessions = g.Count(),
                TotalSeconds = g.Sum(ps => ps.DurationSeconds ?? 0)
            })
            .ToListAsync();

        Servers = servers.Select(s =>
        {
            var stats = sessionStats.FirstOrDefault(x => x.ServerId == s.Id);
            return new ServerViewModel(
                s.Id, s.Name, s.Description, s.Host, s.Port, s.IsActive, s.ApiKey,
                stats?.TotalSessions ?? 0,
                stats?.TotalSeconds ?? 0,
                s.CreatedAt);
        }).ToList();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        // Preserve binding errors for Input (e.g., non-numeric port) before clearing EditHost noise
        var inputErrors = ModelState
            .Where(kv => kv.Key.StartsWith("Input") && kv.Value?.ValidationState == ModelValidationState.Invalid)
            .SelectMany(kv => kv.Value!.Errors.Select(e => (kv.Key, e.ErrorMessage)))
            .ToList();

        ModelState.Clear();
        foreach (var (key, msg) in inputErrors)
            ModelState.AddModelError(key, msg);

        if (!TryValidateModel(Input, nameof(Input)))
        {
            await OnGetAsync();
            return Page();
        }

        db.MinecraftServers.Add(new MinecraftServer
        {
            Name = Input.Name,
            Description = Input.Description,
            Host = Input.Host.Trim(),
            Port = Input.Port,
            ApiKey = GenerateApiKey(),
        });
        await db.SaveChangesAsync();
        StatusMessage = $"Server '{Input.Name}' added.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditHostAsync(int id)
    {
        var editErrors = ModelState
            .Where(kv => kv.Key.StartsWith("EditHost") && kv.Value?.ValidationState == ModelValidationState.Invalid)
            .SelectMany(kv => kv.Value!.Errors.Select(e => (kv.Key, e.ErrorMessage)))
            .ToList();

        ModelState.Clear();
        foreach (var (key, msg) in editErrors)
            ModelState.AddModelError(key, msg);

        if (!TryValidateModel(EditHost, nameof(EditHost)))
        {
            await OnGetAsync();
            return Page();
        }

        var server = await db.MinecraftServers.FindAsync(id);
        if (server is not null)
        {
            server.Host = EditHost.Host.Trim();
            server.Port = EditHost.Port;
            await db.SaveChangesAsync();
            StatusMessage = $"Host updated for '{server.Name}'.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var server = await db.MinecraftServers.FindAsync(id);
        if (server is not null)
        {
            server.IsActive = !server.IsActive;
            await db.SaveChangesAsync();
            StatusMessage = $"'{server.Name}' is now {(server.IsActive ? "active" : "inactive")}.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRegenerateKeyAsync(int id)
    {
        var server = await db.MinecraftServers.FindAsync(id);
        if (server is not null)
        {
            server.ApiKey = GenerateApiKey();
            await db.SaveChangesAsync();
            StatusMessage = $"API key regenerated for '{server.Name}'.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var server = await db.MinecraftServers.FindAsync(id);
        if (server is not null)
        {
            db.MinecraftServers.Remove(server);
            await db.SaveChangesAsync();
            StatusMessage = $"'{server.Name}' removed.";
        }
        return RedirectToPage();
    }

    private static string GenerateApiKey() =>
        Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
}
