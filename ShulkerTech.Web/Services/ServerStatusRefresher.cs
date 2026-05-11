using System.Collections.Concurrent;
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

    // Tracks how many pings in a row have failed per server.
    // A single transient failure is suppressed; two consecutive failures confirm a real outage.
    private readonly ConcurrentDictionary<int, int> _consecutiveFailures = new();

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

            // Phase 1: ping all servers in parallel (pure I/O, no shared state written)
            var pingResults = await Task.WhenAll(servers.Select(async s =>
            {
                var result = await ping.PingAsync(s.Host!, s.Port, ct);
                return (Server: s, Result: result);
            }));

            // Phase 2: apply results sequentially (DbContext is not thread-safe)
            foreach (var (s, result) in pingResults)
            {
                if (result.IsOnline)
                {
                    _consecutiveFailures.TryRemove(s.Id, out _);

                    cache.Set(new CachedServerStatus(s.Id, s.Name, s.Host, s.Port,
                        true, result.PlayersOnline, result.PlayersMax,
                        result.MotdHtml, result.FaviconDataUrl, now,
                        result.OnlinePlayers ?? []));

                    db.ServerPingLogs.Add(new ServerPingLog
                    {
                        ServerId = s.Id, Timestamp = now,
                        IsOnline = true, PlayersOnline = result.PlayersOnline, PlayersMax = result.PlayersMax,
                    });
                }
                else
                {
                    var failures = _consecutiveFailures.AddOrUpdate(s.Id, 1, (_, n) => n + 1);
                    var existing = cache.TryGet(s.Id);

                    if (failures == 1 && existing?.IsOnline == true)
                    {
                        // First failure on a server that was previously online — could be a
                        // transient blip. Preserve the last known good state and skip the log.
                        cache.Set(existing with { LastChecked = now });
                    }
                    else
                    {
                        // Confirmed outage (2+ failures), new server with no cached state yet,
                        // or server was already shown as offline. Show as offline in the UI.
                        cache.Set(new CachedServerStatus(s.Id, s.Name, s.Host, s.Port,
                            false, 0, 0, null, null, now, []));

                        // Only write a log entry when transitioning to offline (not for every
                        // repeated offline ping after the outage is already recorded).
                        if (existing?.IsOnline != false)
                        {
                            db.ServerPingLogs.Add(new ServerPingLog
                            {
                                ServerId = s.Id, Timestamp = now,
                                IsOnline = false, PlayersOnline = 0, PlayersMax = 0,
                            });
                        }
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            // Phase 3: recompute stats (after logs are persisted)
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
            .OrderBy(l => l.Timestamp)
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

        // Current uptime streak — walk backwards through the 7-day window to find the start
        // of the current unbroken online run. Isolated single offline pings (transient failures)
        // are treated as noise so they don't reset a real streak.
        var currentlyOnline = logs7d.Count > 0 && logs7d[^1].IsOnline;
        TimeSpan? streak = null;
        if (currentlyOnline)
        {
            var streakStart = FindStreakStart(logs7d.Select(l => (l.Timestamp, l.IsOnline)));
            if (streakStart.HasValue)
            {
                // If the streak runs all the way to the edge of our 7-day window, the server
                // may have been up even longer — extend to its very first log entry.
                var windowEdge = logs7d[0].Timestamp;
                if (streakStart.Value <= windowEdge.AddMinutes(1))
                {
                    var firstEver = await db.ServerPingLogs
                        .Where(l => l.ServerId == serverId)
                        .MinAsync(l => (DateTime?)l.Timestamp, ct);
                    if (firstEver.HasValue && firstEver.Value < streakStart.Value)
                        streakStart = firstEver.Value;
                }
                streak = now - streakStart.Value;
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

    /// <summary>
    /// Finds the start of the current unbroken online run by walking backwards through logs.
    /// A single isolated offline ping surrounded by online pings is treated as noise.
    /// Two or more consecutive offline pings mark the end of the streak.
    /// </summary>
    /// <param name="logsAscending">Ping logs ordered oldest→newest.</param>
    internal static DateTime? FindStreakStart(IEnumerable<(DateTime Timestamp, bool IsOnline)> logsAscending)
    {
        // Reverse so we walk newest → oldest
        var logsDesc = logsAscending.Reverse().ToList();
        if (logsDesc.Count == 0 || !logsDesc[0].IsOnline)
            return null;

        DateTime? streakStart = null;
        int offlineRun = 0;

        foreach (var (ts, isOnline) in logsDesc)
        {
            if (isOnline)
            {
                offlineRun = 0;
                streakStart = ts;
            }
            else
            {
                offlineRun++;
                if (offlineRun >= 2)
                    break; // real outage found — streak starts at whatever streakStart is
            }
        }

        return streakStart;
    }
}
