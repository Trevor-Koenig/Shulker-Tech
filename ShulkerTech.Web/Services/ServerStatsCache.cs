using System.Collections.Concurrent;

namespace ShulkerTech.Web.Services;

/// <summary>One hourly bucket in the 24-hour sparkline.</summary>
public record PlayerCountSample(
    bool HasData,   // false = no pings recorded for this hour
    bool IsOnline,  // true if any pings in this bucket were online
    int Players);   // average player count across online pings (0 if none)

public record CachedServerStats(
    int ServerId,
    double UptimePercent24h,
    double UptimePercent7d,
    int PeakPlayersAllTime,
    double AvgPlayers7d,
    TimeSpan? CurrentStreak,            // null when server is offline
    IReadOnlyList<PlayerCountSample> History24h); // 24 hourly buckets, oldest → newest

public class ServerStatsCache
{
    private readonly ConcurrentDictionary<int, CachedServerStats> _stats = new();

    public CachedServerStats? Get(int serverId) =>
        _stats.TryGetValue(serverId, out var s) ? s : null;

    public void Set(CachedServerStats stats) => _stats[stats.ServerId] = stats;

    public void Remove(int serverId) => _stats.TryRemove(serverId, out _);
}
