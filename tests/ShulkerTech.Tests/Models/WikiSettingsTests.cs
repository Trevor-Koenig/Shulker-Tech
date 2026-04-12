using FluentAssertions;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Tests.Models;

[Trait("Category", "Unit")]
public class WikiSettingsTests
{
    // Convenience wrappers to keep tests concise
    private static bool Satisfies(string? required, IList<string> roles, bool isAdmin = false)
        => WikiSettings.UserSatisfies(required, roles, isAdmin);

    [Fact]
    public void UserSatisfies_NullRequiredRole_AlwaysReturnsTrue()
    {
        Satisfies(null, [], isAdmin: false).Should().BeTrue();
        Satisfies(null, ["Member"], isAdmin: false).Should().BeTrue();
    }

    [Fact]
    public void UserSatisfies_IsAdminTrue_AlwaysPasses()
    {
        Satisfies("Admin", [], isAdmin: true).Should().BeTrue();
        Satisfies("Moderator", [], isAdmin: true).Should().BeTrue();
        Satisfies("Member", [], isAdmin: true).Should().BeTrue();
    }

    [Fact]
    public void UserSatisfies_MemberRequired_UserHasMember_ReturnsTrue()
    {
        Satisfies("Member", ["Member"]).Should().BeTrue();
    }

    [Fact]
    public void UserSatisfies_ModeratorRequired_UserHasMember_ReturnsFalse()
    {
        Satisfies("Moderator", ["Member"]).Should().BeFalse();
    }

    [Fact]
    public void UserSatisfies_ModeratorRequired_UserHasModerator_ReturnsTrue()
    {
        Satisfies("Moderator", ["Moderator"]).Should().BeTrue();
    }

    [Fact]
    public void UserSatisfies_ModeratorRequired_UserHasAdmin_ReturnsTrueViaHierarchy()
    {
        // Admin role has higher rank than Moderator
        Satisfies("Moderator", ["Admin"]).Should().BeTrue();
    }

    [Fact]
    public void UserSatisfies_AdminRequired_UserHasModerator_ReturnsFalse()
    {
        Satisfies("Admin", ["Moderator"]).Should().BeFalse();
    }

    [Fact]
    public void UserSatisfies_UnknownRole_TreatedAsRank0_FailsAnyRequirement()
    {
        // A user with only an unrecognized role cannot satisfy any requirement
        Satisfies("Member", ["SuperAdmin"]).Should().BeFalse();
    }

    [Fact]
    public void UserSatisfies_EmptyRolesList_NonNullRequirement_ReturnsFalse()
    {
        Satisfies("Member", []).Should().BeFalse();
    }
}
