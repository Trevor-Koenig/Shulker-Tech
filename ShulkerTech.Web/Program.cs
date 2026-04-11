using Markdig;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Core.Services;
using ShulkerTech.Web.Hubs;
using ShulkerTech.Web.Middleware;
using ShulkerTech.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsAssembly("ShulkerTech.Web")));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.AddHttpClient<MojangService>();
builder.Services.AddSingleton<MinecraftPingService>();
builder.Services.AddSingleton<ServerStatusCache>();
builder.Services.AddHostedService<ServerStatusRefresher>();
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Markdown pipeline — advanced extensions enabled, raw HTML stripped for safety
builder.Services.AddSingleton(new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .DisableHtml()
    .Build());

// Scope auth cookies (and antiforgery) to the root domain so they are shared
// across all subdomains (wiki., admin., etc.) without hardcoding any domain name.
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.OnAppendCookie = ctx =>
    {
        var host = ctx.Context.Request.Host.Host;
        var firstSegment = host.Split('.')[0];
        string[] knownSubdomains = ["wiki", "admin"];
        var rootHost = knownSubdomains.Contains(firstSegment, StringComparer.OrdinalIgnoreCase)
            ? host[(firstSegment.Length + 1)..]
            : host;

        // Browsers reject a Domain attribute of "localhost" — leave it unset so the
        // cookie is still sent for localhost requests but won't break anything.
        if (!rootHost.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            ctx.CookieOptions.Domain = $".{rootHost}";
    };
});

var app = builder.Build();

// Auto-apply pending migrations and seed roles on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "Moderator", "Member" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCookiePolicy();
app.UseMiddleware<FirstRunMiddleware>();
app.UseStatusCodePagesWithReExecute("/404");
app.UseMiddleware<SubdomainRoutingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<AdminGuardMiddleware>();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapControllers();
app.MapHub<ServerStatusHub>("/hubs/server-status");

app.Run();
