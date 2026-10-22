namespace BeautyAgenda.Domain.Entities;

public class ProfessionalService
{
    public int ProfessionalId { get; set; }
    public int ServiceId { get; set; }

    public Professional Professional { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
