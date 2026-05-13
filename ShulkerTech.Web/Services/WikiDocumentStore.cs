using System.Collections.Concurrent;

namespace ShulkerTech.Web.Services;

// Tracks per-article in-flight content and active editor presence for collaborative editing.
// Cleared on article save so the next editing session starts from the fresh DB content.
public class WikiDocumentStore
{
    // articleId → latest in-flight content broadcast by any editor
    private readonly ConcurrentDictionary<int, string> _content = new();

    // articleId → (connectionId → displayName)
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, string>> _editors = new();

    // connectionId → articleId (for cleanup on disconnect without knowing the article)
    private readonly ConcurrentDictionary<string, int> _connToArticle = new();

    public string? GetContent(int articleId) =>
        _content.TryGetValue(articleId, out var c) ? c : null;

    public void SetContent(int articleId, string content) =>
        _content[articleId] = content;

    public void ClearContent(int articleId) =>
        _content.TryRemove(articleId, out _);

    public void Track(int articleId, string connectionId, string displayName)
    {
        _editors.GetOrAdd(articleId, _ => new ConcurrentDictionary<string, string>())
                [connectionId] = displayName;
        _connToArticle[connectionId] = articleId;
    }

    // Returns the (articleId, displayName) that were removed, or (-1, null) if unknown.
    public (int ArticleId, string? DisplayName) Untrack(string connectionId)
    {
        if (!_connToArticle.TryRemove(connectionId, out var articleId))
            return (-1, null);

        if (_editors.TryGetValue(articleId, out var map))
        {
            map.TryRemove(connectionId, out var name);
            if (map.IsEmpty) _editors.TryRemove(articleId, out _);
            return (articleId, name);
        }
        return (articleId, null);
    }

    public IReadOnlyList<string> GetEditorNames(int articleId) =>
        _editors.TryGetValue(articleId, out var map) ? map.Values.ToList() : [];
}
