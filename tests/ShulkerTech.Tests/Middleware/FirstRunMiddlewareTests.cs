using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ShulkerTech.Core.Models;
using ShulkerTech.Web.Middleware;

namespace ShulkerTech.Tests.Middleware;

/// <summary>
/// Unit-style tests for FirstRunMiddleware using a DefaultHttpContext and a mocked
/// UserManager. No WebApplicationFactory or Postgres needed.
/// </summary>
[Trait("Category", "Unit")]
public class FirstRunMiddlewareTests
{
    private static UserManager<ApplicationUser> MockUserManager(bool hasUsers)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var users = hasUsers
            ? new[] { new ApplicationUser() }.AsQueryable()
            : Array.Empty<ApplicationUser>().AsQueryable();
        mgr.Setup(m => m.Users).Returns(users);
        return mgr.Object;
    }

    private static async Task<HttpContext> InvokeAsync(
        FirstRunMiddleware middleware,
        UserManager<ApplicationUser> userManager,
        string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var services = new ServiceCollection();
        services.AddSingleton(userManager);
        context.RequestServices = services.BuildServiceProvider();

        RequestDelegate next = ctx => Task.CompletedTask;
        await middleware.InvokeAsync(context);
        return context;
    }

    [Fact]
    public async Task NoUsers_AnyRequest_RedirectsToSetup()
    {
        var middleware = new FirstRunMiddleware(_ => Task.CompletedTask);
        var context = await InvokeAsync(middleware, MockUserManager(false), "/");
        context.Response.Headers.Location.ToString().Should().Be("/setup");
    }

    [Fact]
    public async Task NoUsers_RequestToSetupPath_PassesThrough()
    {
        var called = false;
        var middleware = new FirstRunMiddleware(_ => { called = true; return Task.CompletedTask; });
        await InvokeAsync(middleware, MockUserManager(false), "/setup");
        called.Should().BeTrue();
    }

    [Fact]
    public async Task NoUsers_RequestToCssPath_PassesThrough()
    {
        var called = false;
        var middleware = new FirstRunMiddleware(_ => { called = true; return Task.CompletedTask; });
        await InvokeAsync(middleware, MockUserManager(false), "/css/app.css");
        called.Should().BeTrue();
    }

    [Fact]
    public async Task NoUsers_RequestToJsPath_PassesThrough()
    {
        var called = false;
        var middleware = new FirstRunMiddleware(_ => { called = true; return Task.CompletedTask; });
        await InvokeAsync(middleware, MockUserManager(false), "/js/app.js");
        called.Should().BeTrue();
    }

    [Fact]
    public async Task NoUsers_RequestToImagesPath_PassesThrough()
    {
        var called = false;
        var middleware = new FirstRunMiddleware(_ => { called = true; return Task.CompletedTask; });
        await InvokeAsync(middleware, MockUserManager(false), "/images/logo.png");
        called.Should().BeTrue();
    }

    [Fact]
    public async Task NoUsers_RequestToFaviconIco_PassesThrough()
    {
        var called = false;
        var middleware = new FirstRunMiddleware(_ => { called = true; return Task.CompletedTask; });
        await InvokeAsync(middleware, MockUserManager(false), "/favicon.ico");
        called.Should().BeTrue();
    }

    [Fact]
    public async Task UsersExist_AnyRequest_DoesNotRedirectToSetup()
    {
        var middleware = new FirstRunMiddleware(_ => Task.CompletedTask);
        var context = await InvokeAsync(middleware, MockUserManager(true), "/");
        context.Response.Headers.Location.ToString().Should().NotBe("/setup");
    }

    [Fact]
    public async Task UsersExist_NextMiddlewareCalled()
    {
        var called = false;
        var middleware = new FirstRunMiddleware(_ => { called = true; return Task.CompletedTask; });
        await InvokeAsync(middleware, MockUserManager(true), "/some-page");
        called.Should().BeTrue();
    }
}
