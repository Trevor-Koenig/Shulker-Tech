using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;
using ShulkerTech.Core.Services;

namespace ShulkerTech.Web.Services;

public class ServerStatusRefresher(
    IServiceScopeFactory scopeFactory,
    ServerStatusCache cache,
    ServerStatsCache statsCache,
    MinecraftPingService ping,
    ILogger<ServerStatusRefresher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
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

            var now = DateTime.UtcNow;

            var tasks = servers.Select(async s =>
            {
                var result = await ping.PingAsync(s.Host!, s.Port, ct);

                cache.Set(new CachedServerStatus(
                    s.Id, s.Name, s.Host, s.Port,
                    result.IsOnline, result.PlayersOnline, result.PlayersMax,
                    result.MotdHtml, result.FaviconDataUrl,
                    now,
                    result.OnlinePlayers ?? []));

                // Persist this ping to the log table
                db.ServerPingLogs.Add(new ServerPingLog
                {
                    ServerId = s.Id,
                    Timestamp = now,
                    IsOnline = result.IsOnline,
                    PlayersOnline = result.PlayersOnline,
                    PlayersMax = result.PlayersMax,
                });
            });

            await Task.WhenAll(tasks);
            await db.SaveChangesAsync(ct);

            // Compute and cache stats for each server (after logs are persisted)
            foreach (var s in servers)
            {
                try
                {
                    var stats = await ComputeStatsAsync(db, s.Id, now, ct);
                    statsCache.Set(stats);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to compute stats for server {ServerId}", s.Id);
                }
            }

            // Remove any servers no longer in the active list
            var activeIds = servers.Select(s => s.Id).ToHashSet();
            foreach (var cached in cache.All.Where(s => !activeIds.Contains(s.ServerId)))
            {
                cache.Remove(cached.ServerId);
                statsCache.Remove(cached.ServerId);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing server status");
        }
    }

    private static async Task<CachedServerStats> ComputeStatsAsync(
        ApplicationDbContext db, int serverId, DateTime now, CancellationToken ct)
    {
        var since24h = now.AddHours(-24);
        var since7d = now.AddDays(-7);

        // Single query for the 7d window (covers 24h too)
        var logs7d = await db.ServerPingLogs
            .Where(l => l.ServerId == serverId && l.Timestamp >= since7d)
            .Select(l => new { l.Timestamp, l.IsOnline, l.PlayersOnline })
            .ToListAsync(ct);

        var logs24h = logs7d.Where(l => l.Timestamp >= since24h).ToList();

        // Uptime percentages
        double uptime24h = logs24h.Count > 0
            ? logs24h.Count(l => l.IsOnline) * 100.0 / logs24h.Count : 0;
        double uptime7d = logs7d.Count > 0
            ? logs7d.Count(l => l.IsOnline) * 100.0 / logs7d.Count : 0;

        // All-time peak player count
        int peak = await db.ServerPingLogs
            .Where(l => l.ServerId == serverId)
            .MaxAsync(l => (int?)l.PlayersOnline, ct) ?? 0;

        // Average player count over the last 7 days (online pings only)
        var onlineLogs7d = logs7d.Where(l => l.IsOnline).ToList();
        double avg7d = onlineLogs7d.Count > 0 ? onlineLogs7d.Average(l => l.PlayersOnline) : 0;

        // Current uptime streak — time since the last offline ping
        var currentlyOnline = logs24h.OrderByDescending(l => l.Timestamp).FirstOrDefault()?.IsOnline ?? false;
        TimeSpan? streak = null;
        if (currentlyOnline)
        {
            var lastOffline = await db.ServerPingLogs
                .Where(l => l.ServerId == serverId && !l.IsOnline)
                .MaxAsync(l => (DateTime?)l.Timestamp, ct);

            if (lastOffline.HasValue)
            {
                streak = now - lastOffline.Value;
            }
            else
            {
                // Never recorded offline — streak from the very first log
                var firstLog = await db.ServerPingLogs
                    .Where(l => l.ServerId == serverId)
                    .MinAsync(l => (DateTime?)l.Timestamp, ct);
                streak = firstLog.HasValue ? now - firstLog.Value : TimeSpan.Zero;
            }
        }

        // 24-hour sparkline — 24 hourly buckets, oldest first
        var history = Enumerable.Range(0, 24).Select(h =>
        {
            var bucketStart = since24h.AddHours(h);
            var bucketEnd = bucketStart.AddHours(1);
            var bucket = logs24h
                .Where(l => l.Timestamp >= bucketStart && l.Timestamp < bucketEnd)
                .ToList();

            if (bucket.Count == 0)
                return new PlayerCountSample(false, false, 0);

            var onlineBucket = bucket.Where(l => l.IsOnline).ToList();
            bool anyOnline = onlineBucket.Count > 0;
            int avgPlayers = anyOnline ? (int)Math.Round(onlineBucket.Average(l => l.PlayersOnline)) : 0;
            return new PlayerCountSample(true, anyOnline, avgPlayers);
        }).ToList();

        return new CachedServerStats(serverId, uptime24h, uptime7d, peak, avg7d, streak, history);
    }
}
