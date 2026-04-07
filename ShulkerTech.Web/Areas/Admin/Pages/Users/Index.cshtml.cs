using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Areas.Admin.Pages.Users;

public class IndexModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public List<UserRow> Users { get; set; } = [];

    public record UserRow(ApplicationUser User, IList<string> Roles);

    public async Task OnGetAsync()
    {
        var users = await userManager.Users
            .OrderBy(u => u.UserName)
            .ToListAsync();

        foreach (var user in users)
            Users.Add(new UserRow(user, await userManager.GetRolesAsync(user)));
    }
}
