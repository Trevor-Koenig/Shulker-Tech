using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Core.Data;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class InviteCodeUsageTests(RegisterEmailTestsFactory factory)
    : IClassFixture<RegisterEmailTestsFactory>
{
    private HttpClient CreateClient() =>
        factory.CreateClient(new() { AllowAutoRedirect = false });

    private async Task<FormUrlEncodedContent> RegistrationForm(string code, string? email = null, string? mcUsername = null)
    {
        email ??= $"reg-{Guid.NewGuid():N}@example.com";
        mcUsername ??= $"Player{Guid.NewGuid():N}"[..10];
        factory.SetupMojangReturnsProfile(mcUsername);

        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"]             = email,
            ["Input.Password"]          = "Test@1234!",
            ["Input.ConfirmPassword"]   = "Test@1234!",
            ["Input.MinecraftUsername"] = mcUsername,
            ["Input.InviteCode"]        = code,
        });
    }

    [Fact]
    public async Task Register_WithValidCode_SetsRedeemedById()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var code = await TestDbHelper.CreateInviteCodeAsync(db);

        await CreateClient().PostAsync("/Identity/Account/Register",
            await RegistrationForm(code));

        db.ChangeTracker.Clear();
        var invite = await db.InviteCodes
            .Include(c => c.RedeemedBy)
            .FirstAsync(c => c.Code == code);

        invite.RedeemedById.Should().NotBeNull();
        invite.RedeemedBy.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_WithValidCode_SetsRedeemedAt()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var code = await TestDbHelper.CreateInviteCodeAsync(db);
        var before = DateTime.UtcNow;

        await CreateClient().PostAsync("/Identity/Account/Register",
            await RegistrationForm(code));

        db.ChangeTracker.Clear();
        var invite = await db.InviteCodes.FirstAsync(c => c.Code == code);
        invite.RedeemedAt.Should().NotBeNull();
        invite.RedeemedAt!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task Register_WithInvalidCode_DoesNotSetRedeemedBy()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var code = await TestDbHelper.CreateInviteCodeAsync(db);

        await CreateClient().PostAsync("/Identity/Account/Register",
            await RegistrationForm("BADCODE00"));

        var invite = await db.InviteCodes.FirstAsync(c => c.Code == code);
        invite.RedeemedById.Should().BeNull();
    }

    [Fact]
    public async Task AdminInvitesPage_ShowsRedeemerUsername_AfterCodeUsed()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var code = await TestDbHelper.CreateInviteCodeAsync(db);
        var mcName = $"Redeemer{Guid.NewGuid():N}"[..12];
        factory.SetupMojangReturnsProfile(mcName);

        await CreateClient().PostAsync("/Identity/Account/Register",
            await RegistrationForm(code, mcUsername: mcName));

        var admin = await TestDbHelper.CreateUserAsync(scope.ServiceProvider, isAdmin: true, role: "Admin");
        var adminClient = factory.CreateClient(new() { AllowAutoRedirect = false });
        adminClient.DefaultRequestHeaders.Add("X-Test-User-Id", admin.Id);

        var html = await adminClient.GetStringAsync("/Admin/Invites");
        html.Should().Contain(mcName);
    }

    [Fact]
    public async Task Register_WithRevokedCode_DoesNotSetRedeemedBy()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var code = await TestDbHelper.CreateInviteCodeAsync(db);
        var invite = await db.InviteCodes.FirstAsync(c => c.Code == code);
        invite.IsRevoked = true;
        await db.SaveChangesAsync();

        await CreateClient().PostAsync("/Identity/Account/Register",
            await RegistrationForm(code));

        await db.Entry(invite).ReloadAsync();
        invite.RedeemedById.Should().BeNull();
    }
}
