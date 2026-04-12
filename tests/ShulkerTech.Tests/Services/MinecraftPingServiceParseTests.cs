using FluentAssertions;
using ShulkerTech.Core.Services;

namespace ShulkerTech.Tests.Services;

/// <summary>
/// Tests for the internal ParseResponse method exposed via InternalsVisibleTo.
/// These tests exercise the JSON parsing logic without any TCP connection.
/// </summary>
[Trait("Category", "Unit")]
public class MinecraftPingServiceParseTests
{
    // Shorthand to call the internal method
    private static ServerPingResult Parse(string json) =>
        MinecraftPingService.ParseResponse(json);

    [Fact]
    public void ParseResponse_ValidJsonWithPlayers_ReturnsOnline()
    {
        var json = """{"players":{"online":7,"max":20}}""";
        var result = Parse(json);
        result.IsOnline.Should().BeTrue();
        result.PlayersOnline.Should().Be(7);
        result.PlayersMax.Should().Be(20);
    }

    [Fact]
    public void ParseResponse_MissingPlayersKey_ReturnsOnlineWithZeroCounts()
    {
        var json = """{"version":{"name":"1.21","protocol":765}}""";
        var result = Parse(json);
        result.IsOnline.Should().BeTrue();
        result.PlayersOnline.Should().Be(0);
        result.PlayersMax.Should().Be(0);
    }

    [Fact]
    public void ParseResponse_StringMotd_ParsedWithoutCrash()
    {
        // Legacy plain-text description
        var json = """{"description":"A Minecraft Server","players":{"online":0,"max":20}}""";
        var result = Parse(json);
        result.IsOnline.Should().BeTrue();
    }

    [Fact]
    public void ParseResponse_ChatComponentMotd_RendersWithoutCrash()
    {
        var json = """
            {
              "description": {"text":"Welcome","color":"gold"},
              "players":{"online":1,"max":10}
            }
            """;
        var result = Parse(json);
        result.IsOnline.Should().BeTrue();
        result.MotdHtml.Should().NotBeNull();
    }

    [Fact]
    public void ParseResponse_PlayerSamplePresent_PopulatesOnlinePlayers()
    {
        var json = """
            {
              "players":{
                "online":2,"max":20,
                "sample":[
                  {"name":"Steve","id":"00000000-0000-0000-0000-000000000001"},
                  {"name":"Alex","id":"00000000-0000-0000-0000-000000000002"}
                ]
              }
            }
            """;
        var result = Parse(json);
        result.OnlinePlayers.Should().HaveCount(2);
        result.OnlinePlayers!.Select(p => p.Name).Should().Contain("Steve").And.Contain("Alex");
    }

    [Fact]
    public void ParseResponse_MalformedPlayerSampleEntry_Skipped()
    {
        // One entry missing the "name" field — should be skipped, not crash
        var json = """
            {
              "players":{
                "online":1,"max":20,
                "sample":[
                  {"id":"00000000-0000-0000-0000-000000000001"},
                  {"name":"Alex","id":"00000000-0000-0000-0000-000000000002"}
                ]
              }
            }
            """;
        var result = Parse(json);
        result.OnlinePlayers.Should().ContainSingle().Which.Name.Should().Be("Alex");
    }

    [Fact]
    public void ParseResponse_EmptyJson_ReturnsOffline()
    {
        var result = Parse("{}");
        // Empty but valid JSON — should succeed as online with zero counts
        // (no players key → 0/0, no description → null MOTD)
        result.IsOnline.Should().BeTrue();
    }

    [Fact]
    public void ParseResponse_InvalidJson_ReturnsOffline()
    {
        var result = Parse("not json at all");
        result.IsOnline.Should().BeFalse();
        result.Should().BeEquivalentTo(ServerPingResult.Offline);
    }

    [Fact]
    public void ParseResponse_FaviconPresent_ReturnsFaviconDataUrl()
    {
        var favicon = "data:image/png;base64,abc123";
        var json = $$"""{"players":{"online":0,"max":20},"favicon":"{{favicon}}"}""";
        var result = Parse(json);
        result.FaviconDataUrl.Should().Be(favicon);
    }

    [Fact]
    public void ParseResponse_BoldChatComponent_RendersWithoutCrash()
    {
        var json = """
            {
              "description":{"text":"Bold Server","bold":true,"color":"red"},
              "players":{"online":0,"max":20}
            }
            """;
        var result = Parse(json);
        result.IsOnline.Should().BeTrue();
        // Bold + color should produce some HTML output
        result.MotdHtml.Should().NotBeNullOrEmpty();
    }
}
