namespace BeautyAgenda.Domain.Entities;

public class AdminAuditLog
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Details { get; set; }
}
