using System.Collections.Concurrent;

namespace ShulkerTech.Web.Services;

public record CachedServerStatus(
    int ServerId,
    string Name,
    string? Host,
    int Port,
    bool IsOnline,
    int PlayersOnline,
    int PlayersMax,
    string? Motd,
    string? FaviconDataUrl,
    DateTime LastChecked);

public class ServerStatusCache
{
    private readonly ConcurrentDictionary<int, CachedServerStatus> _statuses = new();

    public IReadOnlyCollection<CachedServerStatus> All => _statuses.Values.OrderBy(s => s.Name).ToList();

    public void Set(CachedServerStatus status) => _statuses[status.ServerId] = status;

    public void Remove(int serverId) => _statuses.TryRemove(serverId, out _);
}
