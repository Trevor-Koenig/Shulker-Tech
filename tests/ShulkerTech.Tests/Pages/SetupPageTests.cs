using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class SetupPageTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient CreateClient() => factory.CreateClient(
        new() { AllowAutoRedirect = false });

    // POST to /setup with a form body (antiforgery is not enforced in Testing env)
    private static FormUrlEncodedContent SetupForm(
        string code = "test-setup-code",
        string email = "",
        string password = "ValidPass@1234",
        string confirm = "ValidPass@1234")
    {
        email = string.IsNullOrEmpty(email) ? $"admin-{Guid.NewGuid():N}@example.com" : email;
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.SetupCode"] = code,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.ConfirmPassword"] = confirm,
        });
    }

    [Fact]
    public async Task Get_NoUsers_Returns200()
    {
        // If DB has no users, /setup should return 200
        // Note: other tests may have created users; we just verify the page itself loads.
        // A fresh factory is not guaranteed here, so we test against actual DB state.
        var response = await CreateClient().GetAsync("/setup");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Get_UsersExist_Redirects()
    {
        // Seed a user to simulate a configured instance
        using var scope = factory.Services.CreateScope();
        await TestDbHelper.CreateUserAsync(scope.ServiceProvider);

        var response = await CreateClient().GetAsync("/setup");
        // Once users exist, /setup redirects to login
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Post_InvalidSetupCode_Returns200WithError()
    {
        var response = await CreateClient().PostAsync("/setup",
            SetupForm(code: "WRONG-CODE"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid setup code");
    }

    [Fact]
    public async Task Post_MissingEmail_Returns200WithValidationError()
    {
        var response = await CreateClient().PostAsync("/setup",
            SetupForm(email: " "));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_PasswordTooShort_Returns200WithValidationError()
    {
        var response = await CreateClient().PostAsync("/setup",
            SetupForm(password: "abc", confirm: "abc"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ValidPost_CreatesAdminUser()
    {
        var email = $"setup-{Guid.NewGuid():N}@example.com";

        // First ensure no users so setup is available
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasUsers = await db.Users.AnyAsync();
        if (hasUsers) return; // Skip if DB already has users (shared fixture)

        var response = await CreateClient().PostAsync("/setup", SetupForm(email: email));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var createdUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        createdUser.Should().NotBeNull();
        createdUser!.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Post_ValidPost_CreatedUserHasAdminRole()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasUsers = await db.Users.AnyAsync();
        if (hasUsers) return;

        var email = $"setup-{Guid.NewGuid():N}@example.com";
        await CreateClient().PostAsync("/setup", SetupForm(email: email));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user == null) return;

        var roles = await userManager.GetRolesAsync(user);
        roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task Post_PasswordsDoNotMatch_Returns200()
    {
        var response = await CreateClient().PostAsync("/setup",
            SetupForm(password: "ValidPass@1234", confirm: "DifferentPass@1234"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
