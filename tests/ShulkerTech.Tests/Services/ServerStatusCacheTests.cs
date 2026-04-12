using FluentAssertions;
using ShulkerTech.Core.Services;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Tests.Services;

[Trait("Category", "Unit")]
public class ServerStatusCacheTests
{
    private static CachedServerStatus MakeStatus(int id, string name) =>
        new(id, name, "localhost", 25565, true, 0, 20, null, null, DateTime.UtcNow, []);

    [Fact]
    public void Set_NewEntry_VisibleInAll()
    {
        var cache = new ServerStatusCache();
        cache.Set(MakeStatus(1, "Alpha"));
        cache.All.Should().ContainSingle(s => s.ServerId == 1);
    }

    [Fact]
    public void Set_ExistingServerId_Overwrites()
    {
        var cache = new ServerStatusCache();
        cache.Set(MakeStatus(1, "Alpha"));
        cache.Set(MakeStatus(1, "Beta"));
        cache.All.Should().ContainSingle().Which.Name.Should().Be("Beta");
    }

    [Fact]
    public void Remove_ExistingEntry_NotVisibleInAll()
    {
        var cache = new ServerStatusCache();
        cache.Set(MakeStatus(1, "Alpha"));
        cache.Remove(1);
        cache.All.Should().BeEmpty();
    }

    [Fact]
    public void Remove_NonExistentKey_DoesNotThrow()
    {
        var cache = new ServerStatusCache();
        var act = () => cache.Remove(999);
        act.Should().NotThrow();
    }

    [Fact]
    public void All_WhenEmpty_ReturnsEmptyCollection()
    {
        var cache = new ServerStatusCache();
        cache.All.Should().BeEmpty();
    }

    [Fact]
    public void All_MultipleEntries_SortedAlphabeticallyByName()
    {
        var cache = new ServerStatusCache();
        cache.Set(MakeStatus(3, "Zebra"));
        cache.Set(MakeStatus(1, "Alpha"));
        cache.Set(MakeStatus(2, "Middle"));

        cache.All.Select(s => s.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public void All_ReturnsSnapshotNotLiveView()
    {
        var cache = new ServerStatusCache();
        cache.Set(MakeStatus(1, "Alpha"));
        var snapshot = cache.All;
        cache.Remove(1);
        // The snapshot captured before the removal should still contain the entry
        snapshot.Should().ContainSingle(s => s.ServerId == 1);
    }

    [Fact]
    public async Task Set_ConcurrentWrites_DoesNotThrow()
    {
        var cache = new ServerStatusCache();
        var tasks = Enumerable.Range(1, 100)
            .Select(i => Task.Run(() => cache.Set(MakeStatus(i, $"Server {i}"))));

        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
        cache.All.Should().HaveCount(100);
    }
}
