using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Trait("Category", "Integration")]
public class RegisterEmailTests(RegisterEmailTestsFactory factory)
    : IClassFixture<RegisterEmailTestsFactory>
{
    private HttpClient CreateClient() =>
        factory.CreateClient(new() { AllowAutoRedirect = false });

    private async Task<FormUrlEncodedContent> ValidFormAsync(
        string? minecraftUsername = null,
        string? inviteCode = null)
    {
        if (inviteCode is null)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            inviteCode = await TestDbHelper.CreateInviteCodeAsync(db);
        }

        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = $"user-{Guid.NewGuid():N}@example.com",
            ["Input.Password"] = "ValidPass@1234",
            ["Input.ConfirmPassword"] = "ValidPass@1234",
            ["Input.MinecraftUsername"] = minecraftUsername ?? "TestPlayer",
            ["Input.InviteCode"] = inviteCode,
        });
    }

    [Fact]
    public async Task Post_ValidRegistration_RedirectsToConfirmationPage()
    {
        factory.SetupMojangReturnsProfile();

        var response = await CreateClient().PostAsync("/Identity/Account/Register", await ValidFormAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("RegisterConfirmation");
    }

    [Fact]
    public async Task Post_ValidRegistration_SendsConfirmationEmail()
    {
        factory.SetupMojangReturnsProfile();
        factory.EmailSenderMock.Reset();

        await CreateClient().PostAsync("/Identity/Account/Register", await ValidFormAsync());

        factory.EmailSenderMock.Verify(
            s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.Is<string>(subject => subject.Contains("Confirm")),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Post_InvalidInviteCode_DoesNotSendEmail()
    {
        factory.EmailSenderMock.Reset();

        var form = await ValidFormAsync(inviteCode: "BADCODE");
        await CreateClient().PostAsync("/Identity/Account/Register", form);

        factory.EmailSenderMock.Verify(
            s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_MojangAccountNotFound_DoesNotSendEmail()
    {
        factory.SetupMojangReturnsNull();
        factory.EmailSenderMock.Reset();

        await CreateClient().PostAsync("/Identity/Account/Register", await ValidFormAsync());

        factory.EmailSenderMock.Verify(
            s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_MinecraftAccountAlreadyLinked_DoesNotSendEmail()
    {
        var uuid = Guid.NewGuid().ToString("N");
        factory.SetupMojangReturnsProfile(uuid: uuid);
        factory.EmailSenderMock.Reset();

        // Seed a user already linked to this UUID
        using var scope = factory.Services.CreateScope();
        await TestDbHelper.CreateUserAsync(scope.ServiceProvider, minecraftUuid: uuid);

        await CreateClient().PostAsync("/Identity/Account/Register", await ValidFormAsync());

        factory.EmailSenderMock.Verify(
            s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}
