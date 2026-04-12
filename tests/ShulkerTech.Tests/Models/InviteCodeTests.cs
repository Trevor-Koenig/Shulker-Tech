using FluentAssertions;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Tests.Models;

[Trait("Category", "Unit")]
public class InviteCodeTests
{
    private static InviteCode Valid() => new()
    {
        Code = "TEST",
        MaxUses = 5,
        UseCount = 2,
        IsRevoked = false,
        ExpiresAt = DateTime.UtcNow.AddDays(1),
    };

    [Fact]
    public void IsValid_NotRevokedUnderMaxUsesNotExpired_ReturnsTrue()
    {
        Valid().IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_Revoked_ReturnsFalse()
    {
        var code = Valid();
        code.IsRevoked = true;
        code.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_UseCountEqualsMaxUses_ReturnsFalse()
    {
        var code = Valid();
        code.UseCount = code.MaxUses;
        code.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_UseCountExceedsMaxUses_ReturnsFalse()
    {
        var code = Valid();
        code.UseCount = code.MaxUses + 1;
        code.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ExpiredByOneSecond_ReturnsFalse()
    {
        var code = Valid();
        code.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        code.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ExpiresAtNull_NeverExpires()
    {
        var code = Valid();
        code.ExpiresAt = null;
        code.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_ExpiresAtInFuture_ReturnsTrue()
    {
        var code = Valid();
        code.ExpiresAt = DateTime.UtcNow.AddYears(1);
        code.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_BothRevokedAndExpired_ReturnsFalse()
    {
        var code = Valid();
        code.IsRevoked = true;
        code.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        code.IsValid.Should().BeFalse();
    }
}
