using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Controllers;

[ApiController]
[Route("api/player")]
public class PlayerApiController(ApplicationDbContext db) : ControllerBase
{
    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] PlayerEventRequest request)
    {
        var server = await ResolveServer();
        if (server is null) return Unauthorized("Invalid API key.");

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.MinecraftUuid == request.MinecraftUuid);
        if (user is null) return NotFound("No registered user with that UUID.");

        // Close any orphaned open session for this player on this server
        var now = DateTime.UtcNow;
        var open = await db.PlayerSessions
            .Where(s => s.UserId == user.Id && s.ServerId == server.Id && s.LeftAt == null)
            .ToListAsync();
        foreach (var s in open)
        {
            s.LeftAt = now;
            s.DurationSeconds = (long)(now - s.JoinedAt).TotalSeconds;
        }

        db.PlayerSessions.Add(new PlayerSession
        {
            UserId = user.Id,
            ServerId = server.Id,
            JoinedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave([FromBody] PlayerEventRequest request)
    {
        var server = await ResolveServer();
        if (server is null) return Unauthorized("Invalid API key.");

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.MinecraftUuid == request.MinecraftUuid);
        if (user is null) return NotFound("No registered user with that UUID.");

        var session = await db.PlayerSessions
            .Where(s => s.UserId == user.Id && s.ServerId == server.Id && s.LeftAt == null)
            .OrderByDescending(s => s.JoinedAt)
            .FirstOrDefaultAsync();

        if (session is null) return NotFound("No open session found.");

        var leftAt = DateTime.UtcNow;
        session.LeftAt = leftAt;
        session.DurationSeconds = (long)(leftAt - session.JoinedAt).TotalSeconds;
        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task<MinecraftServer?> ResolveServer()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var key))
            return null;

        return await db.MinecraftServers
            .FirstOrDefaultAsync(s => s.ApiKey == key.ToString() && s.IsActive);
    }
}

public record PlayerEventRequest(string MinecraftUuid);
