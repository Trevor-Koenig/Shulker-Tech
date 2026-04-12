using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ShulkerTech.Tests.Infrastructure;

namespace ShulkerTech.Tests.Pages;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class AdminPageAuthTests(ShulkerTechWebApplicationFactory factory)
{
    private HttpClient AdminClient(string userId)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);
        return client;
    }

    private HttpClient AnonClient() =>
        factory.CreateClient(new() { AllowAutoRedirect = false });

    private async Task<string> AdminUserId()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider,
            isAdmin: true, role: "Admin");
        return user.Id;
    }

    private async Task<string> NonAdminUserId()
    {
        using var scope = factory.Services.CreateScope();
        var user = await TestDbHelper.CreateUserAsync(scope.ServiceProvider,
            isAdmin: false, role: "Member");
        return user.Id;
    }

    [Fact]
    public async Task AdminIndex_Unauthenticated_Redirects()
    {
        var response = await AnonClient().GetAsync("/Admin");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AdminIndex_NonAdmin_Redirects()
    {
        var userId = await NonAdminUserId();
        var response = await AdminClient(userId).GetAsync("/Admin");
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task AdminIndex_Admin_Returns200()
    {
        var userId = await AdminUserId();
        var response = await AdminClient(userId).GetAsync("/Admin");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminUsers_Admin_Returns200()
    {
        var userId = await AdminUserId();
        var response = await AdminClient(userId).GetAsync("/Admin/Users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminInvites_Admin_Returns200()
    {
        var userId = await AdminUserId();
        var response = await AdminClient(userId).GetAsync("/Admin/Invites");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminServers_Admin_Returns200()
    {
        var userId = await AdminUserId();
        var response = await AdminClient(userId).GetAsync("/Admin/Servers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminMaps_Admin_Returns200()
    {
        var userId = await AdminUserId();
        var response = await AdminClient(userId).GetAsync("/Admin/Maps");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminWikiSettings_Admin_Returns200()
    {
        var userId = await AdminUserId();
        var response = await AdminClient(userId).GetAsync("/Admin/Wiki/Settings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
