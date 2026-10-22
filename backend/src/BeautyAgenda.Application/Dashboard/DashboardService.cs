using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Scheduling;
using BeautyAgenda.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BeautyAgenda.Application.Dashboard;

public sealed record DashboardSummaryDto(
    DateOnly Today,
    int TodayAppointments,
    int NextSevenDaysAppointments,
    int PendingConfirmation,
    decimal MonthCompletedRevenue,
    decimal MonthExpectedRevenue,
    int ActiveProfessionals,
    int TotalCustomers);

public sealed record CalendarAppointmentDto(
    int Id,
    AppointmentStatus Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int ProfessionalId,
    string ProfessionalName,
    string CustomerName,
    string ServicesSummary,
    decimal TotalPrice);

public sealed class DashboardService(IApplicationDbContext db, SalonClock clock)
{
    public const int MaxCalendarRangeDays = 62;

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var today = clock.Today;
        var todayRange = clock.DayRange(today);
        var weekEnd = clock.DayRange(today.AddDays(7)).Start;
        var monthStart = clock.DayRange(new DateOnly(today.Year, today.Month, 1)).Start;
        var monthEnd = clock.DayRange(new DateOnly(today.Year, today.Month, 1).AddMonths(1)).Start;

        var appointments = db.Appointments.AsNoTracking();

        var todayCount = await appointments.CountAsync(a => a.Status != AppointmentStatus.Cancelled
            && a.StartAt >= todayRange.Start && a.StartAt < todayRange.End, ct);
        var weekCount = await appointments.CountAsync(a =>
            (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed)
            && a.StartAt >= now && a.StartAt < weekEnd, ct);
        var pending = await appointments.CountAsync(a => a.Status == AppointmentStatus.Scheduled && a.StartAt >= now, ct);

        var monthServices = db.AppointmentServices.AsNoTracking()
            .Where(s => s.Appointment.StartAt >= monthStart && s.Appointment.StartAt < monthEnd);
        var completed = await monthServices.Where(s => s.Appointment.Status == AppointmentStatus.Completed)
            .SumAsync(s => (decimal?)s.PriceAtBooking, ct) ?? 0m;
        var expected = await monthServices.Where(s => s.Appointment.Status != AppointmentStatus.Cancelled)
            .SumAsync(s => (decimal?)s.PriceAtBooking, ct) ?? 0m;

        var professionals = await db.Professionals.CountAsync(p => p.IsActive, ct);
        var customers = await db.Customers.CountAsync(ct);

        return new DashboardSummaryDto(today, todayCount, weekCount, pending, completed, expected, professionals, customers);
    }

    public async Task<IReadOnlyList<CalendarAppointmentDto>> GetCalendarAsync(
        DateTimeOffset from, DateTimeOffset to, int? professionalId, bool includeCancelled, CancellationToken ct)
    {
        if (to <= from)
            throw Validation.Error("to", "'to' must be after 'from'.");
        if (to - from > TimeSpan.FromDays(MaxCalendarRangeDays))
            throw Validation.Error("to", $"The period cannot exceed {MaxCalendarRangeDays} days.");

        var fromUtc = from.UtcDateTime;
        var toUtc = to.UtcDateTime;

        var items = await db.Appointments.AsNoTracking()
            .Where(a => a.StartAt < toUtc && a.EndAt > fromUtc)
            .Where(a => professionalId == null || a.ProfessionalId == professionalId)
            .Where(a => includeCancelled || a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.StartAt)
            .Select(a => new
            {
                a.Id, a.Status, a.StartAt, a.EndAt, a.ProfessionalId,
                ProfessionalName = a.Professional.Name,
                CustomerName = a.Customer.Name,
                Services = a.Services.OrderBy(s => s.Id).Select(s => s.Service.Name).ToList(),
                Total = a.Services.Sum(s => s.PriceAtBooking),
            })
            .ToListAsync(ct);

        return items.Select(a => new CalendarAppointmentDto(
            a.Id, a.Status, clock.ToLocal(a.StartAt), clock.ToLocal(a.EndAt), a.ProfessionalId,
            a.ProfessionalName, a.CustomerName, string.Join(" + ", a.Services), a.Total)).ToList();
    }
}
