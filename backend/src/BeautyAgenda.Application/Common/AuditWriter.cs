using System.Text.Json;
using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Application.Scheduling;
using BeautyAgenda.Domain.Entities;

namespace BeautyAgenda.Application.Common;

/// <summary>Stages an audit entry; it is persisted by the caller's SaveChanges, in the same transaction.</summary>
public sealed class AuditWriter(IApplicationDbContext db, ICurrentUser currentUser, SalonClock clock)
{
    public void Add(string entityName, object entityId, string action, object? details = null)
    {
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            UserId = currentUser.UserId ?? "unknown",
            EntityName = entityName,
            EntityId = entityId.ToString() ?? string.Empty,
            Action = action,
            CreatedAt = clock.UtcNow,
            Details = details is null ? null : JsonSerializer.Serialize(details),
        });
    }
}
