using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages.Roles;

public class IndexModel(
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext db) : PageModel
{
    public List<IdentityRole> Roles { get; set; } = [];
    public List<SitePermission> Grants { get; set; } = [];
    public ResourceInfo[] Resources { get; set; } = SiteResource.All;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        Roles = await roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
        Grants = await db.SitePermissions.ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateRoleAsync(string roleName)
    {
        roleName = roleName?.Trim() ?? "";
        if (string.IsNullOrEmpty(roleName))
        {
            StatusMessage = "Role name cannot be empty.";
            return RedirectToPage();
        }

        if (await roleManager.RoleExistsAsync(roleName))
        {
            StatusMessage = $"Role \"{roleName}\" already exists.";
            return RedirectToPage();
        }

        var result = await roleManager.CreateAsync(new IdentityRole(roleName));
        StatusMessage = result.Succeeded
            ? $"Role \"{roleName}\" created."
            : $"Failed: {string.Join(", ", result.Errors.Select(e => e.Description))}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteRoleAsync(string roleName)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role == null) return RedirectToPage();

        // Remove all permissions for this role before deleting it
        var perms = db.SitePermissions.Where(p => p.RoleName == roleName);
        db.SitePermissions.RemoveRange(perms);
        await db.SaveChangesAsync();

        var result = await roleManager.DeleteAsync(role);
        StatusMessage = result.Succeeded
            ? $"Role \"{roleName}\" deleted."
            : $"Failed: {string.Join(", ", result.Errors.Select(e => e.Description))}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTogglePermissionAsync(string roleName, string resource)
    {
        var existing = await db.SitePermissions
            .FirstOrDefaultAsync(p => p.RoleName == roleName && p.Resource == resource);

        if (existing != null)
            db.SitePermissions.Remove(existing);
        else
            db.SitePermissions.Add(new SitePermission { RoleName = roleName, Resource = resource });

        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
