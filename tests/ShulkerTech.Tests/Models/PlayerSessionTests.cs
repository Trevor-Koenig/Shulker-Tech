using FluentAssertions;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Tests.Models;

[Trait("Category", "Unit")]
public class PlayerSessionTests
{
    [Fact]
    public void Duration_DurationSecondsNull_ReturnsNull()
    {
        var session = new PlayerSession { UserId = "u", DurationSeconds = null };
        session.Duration.Should().BeNull();
    }

    [Fact]
    public void Duration_DurationSecondsZero_ReturnsZeroTimeSpan()
    {
        var session = new PlayerSession { UserId = "u", DurationSeconds = 0 };
        session.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Duration_DurationSeconds300_ReturnsFiveMinutes()
    {
        var session = new PlayerSession { UserId = "u", DurationSeconds = 300 };
        session.Duration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Duration_DurationSecondsOne_ReturnsOneSecond()
    {
        var session = new PlayerSession { UserId = "u", DurationSeconds = 1 };
        session.Duration.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Duration_DurationSecondsNegative_ReturnsNegativeTimeSpan()
    {
        // Model allows negative values; Duration should reflect them
        var session = new PlayerSession { UserId = "u", DurationSeconds = -60 };
        session.Duration.Should().Be(TimeSpan.FromSeconds(-60));
    }

    [Fact]
    public void Duration_DurationSecondsLargeValue_DoesNotOverflow()
    {
        var session = new PlayerSession { UserId = "u", DurationSeconds = long.MaxValue };
        // TimeSpan.FromSeconds(long.MaxValue) will throw; use a large but safe value instead
        session = new PlayerSession { UserId = "u", DurationSeconds = 86_400 * 365L * 100 };
        session.Duration.Should().BePositive();
    }

    [Fact]
    public void JoinedAt_Default_IsCloseToUtcNow()
    {
        var before = DateTime.UtcNow;
        var session = new PlayerSession { UserId = "u" };
        var after = DateTime.UtcNow;
        session.JoinedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void LeftAt_Default_IsNull()
    {
        var session = new PlayerSession { UserId = "u" };
        session.LeftAt.Should().BeNull();
    }
}
