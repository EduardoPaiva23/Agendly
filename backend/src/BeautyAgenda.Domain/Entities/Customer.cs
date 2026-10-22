namespace BeautyAgenda.Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Digits only, normalized. Unique — used to recognize returning customers.</summary>
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = [];
}
