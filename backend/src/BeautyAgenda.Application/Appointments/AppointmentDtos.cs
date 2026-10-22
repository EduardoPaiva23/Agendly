using BeautyAgenda.Application.Scheduling;
using BeautyAgenda.Domain.Entities;

namespace BeautyAgenda.Application.Appointments;

public sealed record AppointmentServiceDto(int ServiceId, string Name, decimal PriceAtBooking, int DurationMinutes);

/// <summary>Customer-facing view, addressed by the secret reference. Contains no personal data.</summary>
public sealed record PublicAppointmentDto(
    string Reference,
    AppointmentStatus Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string ProfessionalName,
    IReadOnlyList<AppointmentServiceDto> Services,
    decimal TotalPrice,
    bool CanCancelOnline);

public sealed record AppointmentDto(
    int Id,
    string Reference,
    AppointmentStatus Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int CustomerId,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    int ProfessionalId,
    string ProfessionalName,
    IReadOnlyList<AppointmentServiceDto> Services,
    decimal TotalPrice,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason);

public static class AppointmentMapping
{
    public static IReadOnlyList<AppointmentServiceDto> MapServices(Appointment a) =>
        a.Services.OrderBy(s => s.Id)
            .Select(s => new AppointmentServiceDto(s.ServiceId, s.Service.Name, s.PriceAtBooking, s.DurationMinutes))
            .ToList();

    /// <remarks>Requires Customer, Professional and Services.Service loaded.</remarks>
    public static AppointmentDto ToDto(this Appointment a, SalonClock clock) => new(
        a.Id,
        a.PublicReference,
        a.Status,
        clock.ToLocal(a.StartAt),
        clock.ToLocal(a.EndAt),
        a.CustomerId,
        a.Customer.Name,
        a.Customer.Phone,
        a.Customer.Email,
        a.ProfessionalId,
        a.Professional.Name,
        MapServices(a),
        a.TotalPrice,
        clock.ToLocal(a.CreatedAt),
        a.UpdatedAt is { } u ? clock.ToLocal(u) : null,
        a.CancelledAt is { } c ? clock.ToLocal(c) : null,
        a.CancellationReason);
}
