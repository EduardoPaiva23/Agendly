using BeautyAgenda.Domain.Common;
using BeautyAgenda.Domain.Scheduling;

namespace BeautyAgenda.Domain.Entities;

public class Appointment
{
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> AllowedTransitions = new()
    {
        [AppointmentStatus.Scheduled] = [AppointmentStatus.Confirmed, AppointmentStatus.Completed, AppointmentStatus.Cancelled],
        [AppointmentStatus.Confirmed] = [AppointmentStatus.Completed, AppointmentStatus.Cancelled],
        [AppointmentStatus.Completed] = [],
        [AppointmentStatus.Cancelled] = [],
    };

    private Appointment() { }

    public int Id { get; private set; }
    /// <summary>Unguessable token used by customers to view/cancel without an account.</summary>
    public string PublicReference { get; private set; } = string.Empty;
    public int CustomerId { get; private set; }
    public int ProfessionalId { get; private set; }
    /// <summary>UTC.</summary>
    public DateTime StartAt { get; private set; }
    /// <summary>UTC.</summary>
    public DateTime EndAt { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public Customer Customer { get; private set; } = null!;
    public Professional Professional { get; private set; } = null!;
    public ICollection<AppointmentService> Services { get; private set; } = [];

    public TimeRange Range => new(StartAt, EndAt);
    public bool IsActive => IsActiveStatus(Status);
    public int TotalDurationMinutes => Services.Sum(s => s.DurationMinutes);
    public decimal TotalPrice => Services.Sum(s => s.PriceAtBooking);

    public static bool IsActiveStatus(AppointmentStatus status) =>
        status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed;

    public static Appointment Create(
        Customer customer,
        int professionalId,
        DateTime startAtUtc,
        IReadOnlyCollection<Service> services,
        string publicReference,
        DateTime nowUtc)
    {
        if (services.Count == 0)
            throw new DomainException("An appointment must include at least one service.");
        if (startAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Start must be UTC.", nameof(startAtUtc));

        var appointment = new Appointment
        {
            PublicReference = publicReference,
            Customer = customer,
            CustomerId = customer.Id,
            ProfessionalId = professionalId,
            StartAt = startAtUtc,
            Status = AppointmentStatus.Scheduled,
            CreatedAt = nowUtc,
        };

        foreach (var service in services)
        {
            appointment.Services.Add(new AppointmentService
            {
                ServiceId = service.Id,
                Service = service,
                PriceAtBooking = service.Price,
                DurationMinutes = service.DurationMinutes,
            });
        }

        appointment.EndAt = startAtUtc.AddMinutes(appointment.TotalDurationMinutes);
        return appointment;
    }

    public static bool CanTransition(AppointmentStatus from, AppointmentStatus to) =>
        AllowedTransitions[from].Contains(to);

    public void ChangeStatus(AppointmentStatus target, DateTime nowUtc, string? reason = null)
    {
        if (target == Status)
            throw new DomainException($"Appointment is already {Status}.");
        if (!CanTransition(Status, target))
            throw new DomainException($"Cannot change appointment status from {Status} to {target}.");

        Status = target;
        UpdatedAt = nowUtc;
        if (target == AppointmentStatus.Cancelled)
        {
            CancelledAt = nowUtc;
            CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        }
    }

    public void Cancel(DateTime nowUtc, string? reason) => ChangeStatus(AppointmentStatus.Cancelled, nowUtc, reason);

    /// <summary>Moves the appointment keeping the services and the prices agreed at booking time.</summary>
    public void Reschedule(int professionalId, DateTime newStartUtc, DateTime nowUtc)
    {
        if (!IsActive)
            throw new DomainException($"A {Status} appointment cannot be rescheduled.");
        if (newStartUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Start must be UTC.", nameof(newStartUtc));

        ProfessionalId = professionalId;
        StartAt = newStartUtc;
        EndAt = newStartUtc.AddMinutes(TotalDurationMinutes);
        UpdatedAt = nowUtc;
    }
}
