using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using ShulkerTech.Core.Models;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Web.Hubs;

[Authorize]
public class WikiEditHub(WikiDocumentStore store, UserManager<ApplicationUser> userManager) : Hub
{
    public async Task JoinDocument(int articleId)
    {
        var user = await userManager.GetUserAsync(Context.User!);
        var displayName = (user?.MinecraftUsername ?? user?.UserName ?? "Unknown").ToUpperInvariant();

        store.Track(articleId, Context.ConnectionId, displayName);
        await Groups.AddToGroupAsync(Context.ConnectionId, DocGroup(articleId));

        // Send caller the latest in-flight content from an active co-editor (if any)
        var content = store.GetContent(articleId);
        if (content is not null)
            await Clients.Caller.SendAsync("ContentSync", content);

        // Tell existing editors who just joined
        await Clients.OthersInGroup(DocGroup(articleId)).SendAsync("EditorJoined", displayName);

        // Send caller the list of currently active co-editors
        var others = store.GetEditorNames(articleId)
            .Where(n => n != displayName)
            .ToList();
        await Clients.Caller.SendAsync("EditorList", others);
    }

    public async Task LeaveDocument(int articleId)
    {
        var (_, displayName) = store.Untrack(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, DocGroup(articleId));
        if (displayName is not null)
            await Clients.OthersInGroup(DocGroup(articleId)).SendAsync("EditorLeft", displayName);
    }

    // Stores the latest content and relays it to co-editors (not back to the sender).
    public async Task BroadcastContent(int articleId, string content)
    {
        store.SetContent(articleId, content);
        await Clients.OthersInGroup(DocGroup(articleId)).SendAsync("ContentSync", content);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var (articleId, displayName) = store.Untrack(Context.ConnectionId);
        if (articleId >= 0 && displayName is not null)
            await Clients.OthersInGroup(DocGroup(articleId)).SendAsync("EditorLeft", displayName);
        await base.OnDisconnectedAsync(exception);
    }

    private static string DocGroup(int articleId) => $"wiki-article-{articleId}";
}
