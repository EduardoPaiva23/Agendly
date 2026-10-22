namespace BeautyAgenda.Domain.Entities;

public class Professional
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProfessionalService> Services { get; set; } = [];
    public ICollection<ProfessionalTimeOff> TimeOffs { get; set; } = [];
    public ICollection<Appointment> Appointments { get; set; } = [];
}
