using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Services;

namespace ShulkerTech.Web.Services;

public class ServerStatusRefresher(
    IServiceScopeFactory scopeFactory,
    ServerStatusCache cache,
    MinecraftPingService ping,
    ILogger<ServerStatusRefresher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Run immediately on startup, then on the interval
        await RefreshAsync(ct);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(ct))
            await RefreshAsync(ct);
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var servers = await db.MinecraftServers
                .Where(s => s.IsActive && s.Host != null)
                .ToListAsync(ct);

            var tasks = servers.Select(async s =>
            {
                var result = await ping.PingAsync(s.Host!, s.Port, ct);
                cache.Set(new CachedServerStatus(
                    s.Id, s.Name, s.Host, s.Port,
                    result.IsOnline, result.PlayersOnline, result.PlayersMax,
                    result.Motd, result.FaviconDataUrl,
                    DateTime.UtcNow));
            });

            await Task.WhenAll(tasks);

            // Remove any servers no longer in the active list
            var activeIds = servers.Select(s => s.Id).ToHashSet();
            foreach (var cached in cache.All.Where(s => !activeIds.Contains(s.ServerId)))
                cache.Remove(cached.ServerId);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing server status");
        }
    }
}
