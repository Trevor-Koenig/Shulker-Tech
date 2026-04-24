using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Models;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class ForcePasswordResetTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient(string? userId = null)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        if (userId != null)
            client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private static FormUrlEncodedContent EmptyForm() => new(new Dictionary<string, string>());

    // Fetch user from a fresh scope so EF Core doesn't return a stale tracked entity.
    private async Task<ApplicationUser> FreshFetchAsync(string userId)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await userManager.FindByIdAsync(userId))!;
    }

    // ── ForcePasswordReset handler ────────────────────────────────────────────

    [Fact]
    public async Task ForcePasswordReset_SetsMustChangePasswordFlag()
    {
        string adminId, targetId;
        using (var scope = factory.Services.CreateScope())
        {
            var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true, role: "Admin");
            var target = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
            adminId = admin.Id;
            targetId = target.Id;
        }

        await CreateClient(adminId)
            .PostAsync($"/Admin/Users/Edit/{targetId}?handler=ForcePasswordReset", EmptyForm());

        var updated = await FreshFetchAsync(targetId);
        updated.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task ForcePasswordReset_LocksAccount()
    {
        string adminId, targetId;
        using (var scope = factory.Services.CreateScope())
        {
            var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true, role: "Admin");
            var target = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
            adminId = admin.Id;
            targetId = target.Id;
        }

        await CreateClient(adminId)
            .PostAsync($"/Admin/Users/Edit/{targetId}?handler=ForcePasswordReset", EmptyForm());

        var updated = await FreshFetchAsync(targetId);
        updated.LockoutEnd.Should().NotBeNull();
        updated.LockoutEnd!.Value.Should().BeAfter(DateTimeOffset.UtcNow.AddYears(99));
    }

    [Fact]
    public async Task ForcePasswordReset_RedirectsToEditPage()
    {
        string adminId, targetId;
        using (var scope = factory.Services.CreateScope())
        {
            var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true, role: "Admin");
            var target = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
            adminId = admin.Id;
            targetId = target.Id;
        }

        var response = await CreateClient(adminId)
            .PostAsync($"/Admin/Users/Edit/{targetId}?handler=ForcePasswordReset", EmptyForm());

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain(targetId);
    }

    [Fact]
    public async Task ForcePasswordReset_NonAdmin_DoesNotAffectTarget()
    {
        string memberId, targetId;
        using (var scope = factory.Services.CreateScope())
        {
            var member = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
            var target = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
            memberId = member.Id;
            targetId = target.Id;
        }

        var response = await CreateClient(memberId)
            .PostAsync($"/Admin/Users/Edit/{targetId}?handler=ForcePasswordReset", EmptyForm());

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var unchanged = await FreshFetchAsync(targetId);
        unchanged.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task ForcePasswordReset_Unauthenticated_Redirects()
    {
        string targetId;
        using (var scope = factory.Services.CreateScope())
        {
            var target = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
            targetId = target.Id;
        }

        var response = await CreateClient()
            .PostAsync($"/Admin/Users/Edit/{targetId}?handler=ForcePasswordReset", EmptyForm());

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    // ── ResetPassword clears MustChangePassword + lockout ────────────────────

    [Fact]
    public async Task ResetPassword_OnSuccess_ClearsMustChangePasswordAndLockout()
    {
        string userId;
        string userEmail;
        string rawToken;

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, role: "Member");
            userId = user.Id;
            userEmail = user.Email!;

            // Simulate the state left by ForcePasswordReset
            user.MustChangePassword = true;
            await userManager.SetLockoutEnabledAsync(user, true);
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await userManager.UpdateAsync(user);

            rawToken = await userManager.GeneratePasswordResetTokenAsync(user);
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"]           = userEmail,
            ["Input.Password"]        = "NewPassword@1234!",
            ["Input.ConfirmPassword"] = "NewPassword@1234!",
            // ResetPassword.OnPost receives the raw (decoded) token in Input.Code
            ["Input.Code"]            = rawToken,
        });

        var response = await CreateClient().PostAsync("/Identity/Account/ResetPassword", form);
        // Successful reset redirects to ResetPasswordConfirmation
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var updated = await FreshFetchAsync(userId);
        updated.MustChangePassword.Should().BeFalse();
        var isStillLocked = updated.LockoutEnd.HasValue && updated.LockoutEnd > DateTimeOffset.UtcNow;
        isStillLocked.Should().BeFalse();
    }

    // ── Lockout page contextual messages ─────────────────────────────────────

    [Fact]
    public async Task LockoutPage_WithMustReset_ShowsPasswordResetMessage()
    {
        var response = await CreateClient().GetAsync("/Identity/Account/Lockout?mustReset=true");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("password reset");
        body.Should().Contain("check your email");
    }

    [Fact]
    public async Task LockoutPage_WithoutMustReset_ShowsGenericLockoutMessage()
    {
        var response = await CreateClient().GetAsync("/Identity/Account/Lockout");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("too many failed");
    }
}
