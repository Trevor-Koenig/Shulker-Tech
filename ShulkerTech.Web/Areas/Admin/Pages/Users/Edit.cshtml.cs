using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages.Users;

public class EditModel(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IEmailSender emailSender) : PageModel
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

    public async Task<IActionResult> OnPostForcePasswordResetAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var callbackUrl = Url.Page(
            "/Account/ResetPassword",
            pageHandler: null,
            values: new { area = "Identity", code },
            protocol: Request.Scheme)!;

        await emailSender.SendEmailAsync(
            user.Email!,
            "Reset your Shulker Tech password",
            $"An admin has requested a password reset for your account. " +
            $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>. " +
            $"If you did not expect this, contact an administrator.");

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        user.MustChangePassword = true;
        await userManager.UpdateAsync(user);

        StatusMessage = $"Password reset email sent to {user.Email}. Account locked until reset is complete.";
        return RedirectToPage(new { id });
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
