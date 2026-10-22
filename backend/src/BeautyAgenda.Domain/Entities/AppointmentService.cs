namespace BeautyAgenda.Domain.Entities;

/// <summary>A service included in an appointment, with price and duration frozen at booking time.</summary>
public class AppointmentService
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public int ServiceId { get; set; }
    public decimal PriceAtBooking { get; set; }
    public int DurationMinutes { get; set; }

    public Appointment Appointment { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
