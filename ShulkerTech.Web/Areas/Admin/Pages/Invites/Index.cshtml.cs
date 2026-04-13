using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages.Invites;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public List<InviteCode> Codes { get; set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public string? Note { get; set; }
        public int MaxUses { get; set; } = 1;
        public DateTime? ExpiresAt { get; set; }
    }

    public async Task OnGetAsync()
    {
        Codes = await db.InviteCodes
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        var code = new InviteCode
        {
            Code = GenerateCode(),
            Note = Input.Note,
            MaxUses = Math.Max(1, Input.MaxUses),
            ExpiresAt = Input.ExpiresAt.HasValue
                ? DateTime.SpecifyKind(Input.ExpiresAt.Value.ToUniversalTime(), DateTimeKind.Utc)
                : null,
        };

        db.InviteCodes.Add(code);
        await db.SaveChangesAsync();
        StatusMessage = $"Code {code.Code} generated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAsync(int id)
    {
        var code = await db.InviteCodes.FindAsync(id);
        if (code is not null)
        {
            code.IsRevoked = true;
            await db.SaveChangesAsync();
            StatusMessage = $"Code {code.Code} revoked.";
        }
        return RedirectToPage();
    }

    private static string GenerateCode()
    {
        // Format: XXXX-XXXX-XXXX — easy to read and share in Discord
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous chars
        var rng = Random.Shared;
        string Segment() => new(Enumerable.Range(0, 4).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
        return $"{Segment()}-{Segment()}-{Segment()}";
    }
}
