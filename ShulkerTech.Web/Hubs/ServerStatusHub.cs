using Microsoft.AspNetCore.SignalR;

namespace ShulkerTech.Web.Hubs;

/// <summary>
/// Central real-time hub. Handles Minecraft server status pushes,
/// player count updates, announcements, and future wiki collaboration.
/// </summary>
public class ServerStatusHub : Hub
{
    public async Task JoinGroup(string group)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
    }

    public async Task LeaveGroup(string group)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
    }
}
