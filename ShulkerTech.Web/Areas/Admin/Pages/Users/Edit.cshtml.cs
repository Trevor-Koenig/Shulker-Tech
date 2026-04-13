using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages.Users;

public class EditModel(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : PageModel
{
    public ApplicationUser? Member { get; set; }
    public IList<string> AssignedRoles { get; set; } = [];
    public IList<string> AllRoles { get; set; } = [];
    public bool IsDeactivated { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        Member = await userManager.FindByIdAsync(id);
        if (Member is null) return NotFound();

        AssignedRoles = await userManager.GetRolesAsync(Member);
        AllRoles = roleManager.Roles.Select(r => r.Name!).OrderBy(r => r).ToList();
        IsDeactivated = Member.LockoutEnd.HasValue && Member.LockoutEnd > DateTimeOffset.UtcNow;
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        StatusMessage = $"{user.UserName} has been deactivated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReactivateAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        await userManager.SetLockoutEndDateAsync(user, null);
        StatusMessage = $"{user.UserName} has been reactivated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAssignAsync(string id, string role)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
            StatusMessage = $"Role {role} assigned to {user.UserName}.";
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemoveAsync(string id, string role)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (await userManager.IsInRoleAsync(user, role))
        {
            await userManager.RemoveFromRoleAsync(user, role);
            StatusMessage = $"Role {role} removed from {user.UserName}.";
        }

        return RedirectToPage(new { id });
    }
}
