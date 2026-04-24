using System.Threading.RateLimiting;
using Markdig;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using ShulkerTech.Web.Markdown;
using Microsoft.AspNetCore.Identity.UI.Services;
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

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
if (builder.Environment.IsDevelopment())
    builder.Services.AddScoped<IEmailSender, DevEmailSender>();
else
    builder.Services.AddHttpClient<IEmailSender, EmailSender>();

builder.Services.AddHttpClient<IMojangService, MojangService>();
builder.Services.AddSingleton<MinecraftPingService>();
builder.Services.AddSingleton<ServerStatusCache>();
builder.Services.AddSingleton<ServerStatsCache>();
builder.Services.AddHostedService<ServerStatusRefresher>();
builder.Services.AddHostedService<DatabaseBackupService>();
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static RateLimitPartition<string> Fixed(string key, int permits, int windowSeconds) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

    // Named policy for the upload API controller (applied via [EnableRateLimiting])
    opts.AddPolicy("upload", ctx =>
        Fixed(ctx.Connection.RemoteIpAddress?.ToString() ?? "anon", 20, 60));

    // Global limiter covers auth pages (Razor Pages can't use [EnableRateLimiting] directly)
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var ip   = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        var path = ctx.Request.Path.Value ?? "";

        if (path.StartsWith("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase))
            return Fixed($"login:{ip}", 10, 60);

        if (path.StartsWith("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase))
            return Fixed($"register:{ip}", 5, 300);

        if (path.StartsWith("/Identity/Account/ForgotPassword", StringComparison.OrdinalIgnoreCase))
            return Fixed($"pwreset:{ip}", 5, 300);

        return RateLimitPartition.GetNoLimiter("none");
    });
});

// Markdown pipeline — advanced extensions enabled, raw HTML stripped for safety
builder.Services.AddSingleton(new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .DisableHtml()
    .Build());
builder.Services.AddSingleton<WikiMarkdownService>();

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
// Skipped in Testing environment — WebApplicationFactory runs migrations directly
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
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
    app.UseHttpsRedirection();
}
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseCookiePolicy();
app.UseMiddleware<FirstRunMiddleware>();
app.UseStatusCodePagesWithReExecute("/404");
app.UseMiddleware<SubdomainRoutingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<AdminGuardMiddleware>();
app.UseMiddleware<Require2FAMiddleware>();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapControllers();
app.MapHub<ServerStatusHub>("/hubs/server-status");

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
