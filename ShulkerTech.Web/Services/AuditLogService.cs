using ShulkerTech.Core.Data;
using ShulkerTech.Core.Models;

namespace ShulkerTech.Web.Services;

public class AuditLogService(ApplicationDbContext db)
{
    public void Log(string action, string actorId, int? articleId = null, string? articleTitle = null)
    {
        db.AuditLog.Add(new AuditLogEntry
        {
            Action       = action,
            ActorId      = actorId,
            ArticleId    = articleId,
            ArticleTitle = articleTitle,
            OccurredAt   = DateTime.UtcNow,
        });
    }
}
