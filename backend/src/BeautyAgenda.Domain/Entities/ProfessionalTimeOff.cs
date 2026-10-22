namespace BeautyAgenda.Domain.Entities;

public class ProfessionalTimeOff
{
    public int Id { get; set; }
    public int ProfessionalId { get; set; }
    /// <summary>UTC.</summary>
    public DateTime StartAt { get; set; }
    /// <summary>UTC.</summary>
    public DateTime EndAt { get; set; }
    public string? Reason { get; set; }

    public Professional Professional { get; set; } = null!;
}
