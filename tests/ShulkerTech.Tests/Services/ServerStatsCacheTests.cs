using FluentAssertions;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Tests.Services;

[Trait("Category", "Unit")]
public class ServerStatsCacheTests
{
    private static CachedServerStats MakeStats(int id) =>
        new(id, 99.5, 98.0, 42, 5.5, TimeSpan.FromHours(2),
            Enumerable.Repeat(new PlayerCountSample(true, true, 3), 24).ToList());

    [Fact]
    public void Get_KeyNotPresent_ReturnsNull()
    {
        var cache = new ServerStatsCache();
        cache.Get(999).Should().BeNull();
    }

    [Fact]
    public void Set_ThenGet_ReturnsSameStats()
    {
        var cache = new ServerStatsCache();
        var stats = MakeStats(1);
        cache.Set(stats);
        cache.Get(1).Should().BeEquivalentTo(stats);
    }

    [Fact]
    public void Remove_ExistingEntry_GetReturnsNull()
    {
        var cache = new ServerStatsCache();
        cache.Set(MakeStats(1));
        cache.Remove(1);
        cache.Get(1).Should().BeNull();
    }

    [Fact]
    public void Remove_NonExistentKey_DoesNotThrow()
    {
        var cache = new ServerStatsCache();
        var act = () => cache.Remove(999);
        act.Should().NotThrow();
    }

    [Fact]
    public void Set_SameId_Overwrites()
    {
        var cache = new ServerStatsCache();
        cache.Set(MakeStats(1));
        var updated = new CachedServerStats(1, 50.0, 50.0, 10, 2.0, null, []);
        cache.Set(updated);
        cache.Get(1)!.UptimePercent24h.Should().Be(50.0);
    }

    [Fact]
    public void Set_MultipleIds_EachRetrievableById()
    {
        var cache = new ServerStatsCache();
        cache.Set(MakeStats(1));
        cache.Set(MakeStats(2));
        cache.Set(MakeStats(3));
        cache.Get(1)!.ServerId.Should().Be(1);
        cache.Get(2)!.ServerId.Should().Be(2);
        cache.Get(3)!.ServerId.Should().Be(3);
    }

    [Fact]
    public void Get_AfterSetAndRemove_ReturnsNull()
    {
        var cache = new ServerStatsCache();
        cache.Set(MakeStats(5));
        cache.Remove(5);
        cache.Get(5).Should().BeNull();
    }
}
