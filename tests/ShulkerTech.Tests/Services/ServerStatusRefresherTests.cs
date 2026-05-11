using FluentAssertions;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Tests.Services;

[Trait("Category", "Unit")]
public class ServerStatusRefresherStreakTests
{
    // Convenience: build a log list from a bool pattern (true=online, false=offline)
    // Timestamps are spaced 30 seconds apart starting from a fixed base.
    private static List<(DateTime Timestamp, bool IsOnline)> Logs(params bool[] pattern)
    {
        var base_ = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return pattern.Select((online, i) => (base_.AddSeconds(i * 30), online)).ToList();
    }

    // ── Basic cases ───────────────────────────────────────────────────────────

    [Fact]
    public void FindStreakStart_AllOnline_ReturnsFirstEntry()
    {
        var logs = Logs(true, true, true, true, true);
        var result = ServerStatusRefresher.FindStreakStart(logs);
        result.Should().Be(logs[0].Timestamp);
    }

    [Fact]
    public void FindStreakStart_EmptyList_ReturnsNull()
    {
        var result = ServerStatusRefresher.FindStreakStart([]);
        result.Should().BeNull();
    }

    [Fact]
    public void FindStreakStart_LastEntryOffline_ReturnsNull()
    {
        // Server is currently offline — no streak
        var logs = Logs(true, true, true, false);
        var result = ServerStatusRefresher.FindStreakStart(logs);
        result.Should().BeNull();
    }

    [Fact]
    public void FindStreakStart_OnlyOneEntry_Online_ReturnsThatEntry()
    {
        var logs = Logs(true);
        var result = ServerStatusRefresher.FindStreakStart(logs);
        result.Should().Be(logs[0].Timestamp);
    }

    // ── Real outage detection ─────────────────────────────────────────────────

    [Fact]
    public void FindStreakStart_TwoConsecutiveOffline_StreakStartsAfterOutage()
    {
        // Server was offline at [1] and [2], came back at [3]
        var logs = Logs(true, false, false, true, true, true);
        var result = ServerStatusRefresher.FindStreakStart(logs);
        result.Should().Be(logs[3].Timestamp, "streak starts at first online entry after the real outage");
    }

    [Fact]
    public void FindStreakStart_MultipleConsecutiveOffline_StreakStartsAfterOutage()
    {
        var logs = Logs(true, true, false, false, false, true, true);
        var result = ServerStatusRefresher.FindStreakStart(logs);
        result.Should().Be(logs[5].Timestamp);
    }

    [Fact]
    public void FindStreakStart_OutageAtStart_StreakStartsAfterOutage()
    {
        var logs = Logs(false, false, true, true, true);
        var result = ServerStatusRefresher.FindStreakStart(logs);
        result.Should().Be(logs[2].Timestamp);
    }

    // ── Transient / isolated failure handling ─────────────────────────────────

    [Fact]
    public void FindStreakStart_SingleIsolatedOffline_TreatedAsNoise()
    {
        // One bad ping in the middle — should not reset the streak
        var logs = Logs(true, true, false, true, true);
        var result = ServerStatusRefresher.FindStreakStart(logs);
        result.Should().Be(logs[0].Timestamp,
            "a single isolated offline ping should be ignored");
    }

    [Fact]
    public void FindStreakStart_TwoSeparateIsolatedOffline_BothTreatedAsNoise()
    {
        var logs = Logs(true, false, true, true, false, true, true);
        var result = ServerStatusRefresher.FindStreakStart(logs);
        result.Should().Be(logs[0].Timestamp,
            "two separate isolated failures are both noise");
    }

    [Fact]
    public void FindStreakStart_IsolatedOfflineAtStart_TreatedAsNoise()
    {
        // First ping failed but everything else is online — should not prevent streak
        var logs = Logs(false, true, true, true, true);
        var result = ServerStatusRefresher.FindStreakStart(logs);
        result.Should().Be(logs[1].Timestamp,
            "isolated offline at the start is noise; streak starts at next online entry");
    }

    [Fact]
    public void FindStreakStart_IsolatedOfflineJustBeforeCurrent_TreatedAsNoise()
    {
        // Transient failure on the ping just before the current one
        var logs = Logs(true, true, true, false, true);
        var result = ServerStatusRefresher.FindStreakStart(logs);
        result.Should().Be(logs[0].Timestamp);
    }

    // ── Edge: real outage then isolated failure after recovery ─────────────────

    [Fact]
    public void FindStreakStart_RealOutageThenIsolatedFailure_StreakStartsAtRecovery()
    {
        // Real outage [1][2], recovery at [3], isolated blip at [4], back online [5][6]
        var logs = Logs(true, false, false, true, false, true, true);
        var result = ServerStatusRefresher.FindStreakStart(logs);
        // Walks backwards: [6]=on, [5]=on (offlineRun reset), [4]=off (offlineRun=1, noise),
        // [3]=on (offlineRun reset, streakStart=[3]), [2]=off (offlineRun=1), [1]=off (offlineRun=2) → break
        result.Should().Be(logs[3].Timestamp,
            "streak starts at recovery after the real outage; isolated blip after recovery is noise");
    }

    // ── Streak duration grows correctly ───────────────────────────────────────

    [Fact]
    public void FindStreakStart_LongAllOnline_DurationMatchesFirstEntry()
    {
        // 100 online entries at 30s intervals = 50 minutes
        var many = Enumerable.Repeat(true, 100).ToList();
        var logs = Logs([.. many]);
        var start = ServerStatusRefresher.FindStreakStart(logs);
        start.Should().Be(logs[0].Timestamp);
        var duration = logs[^1].Timestamp - start!.Value;
        duration.Should().BeCloseTo(TimeSpan.FromMinutes(49.5), TimeSpan.FromSeconds(1));
    }
}
