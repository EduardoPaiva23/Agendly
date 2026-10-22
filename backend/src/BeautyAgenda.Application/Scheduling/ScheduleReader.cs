using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Domain.Entities;
using BeautyAgenda.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeautyAgenda.Application.Scheduling;

public readonly record struct BookingWindow(DateTime EarliestStart, DateTime LatestStart);

/// <summary>Loads the inputs of <see cref="AvailabilityCalculator"/> from the database.</summary>
public sealed class ScheduleReader(IApplicationDbContext db, SalonClock clock, IOptions<SchedulingOptions> options)
{
    private readonly SchedulingOptions _options = options.Value;

    public TimeSpan SlotStep => TimeSpan.FromMinutes(_options.SlotIntervalMinutes);

    /// <summary>Policy for customers booking online: minimum notice and maximum horizon.</summary>
    public BookingWindow PublicWindow()
    {
        var now = clock.UtcNow;
        return new(now.AddMinutes(_options.MinAdvanceMinutes), now.AddDays(_options.MaxAdvanceDays));
    }

    /// <summary>Policy for staff: no minimum notice, but never in the past.</summary>
    public BookingWindow StaffWindow()
    {
        var now = clock.UtcNow;
        return new(now, now.AddDays(_options.MaxAdvanceDays));
    }

    /// <summary>Salon working intervals on a local date, as UTC ranges.</summary>
    public async Task<IReadOnlyList<TimeRange>> GetWorkingRangesAsync(DateOnly date, CancellationToken ct)
    {
        var hours = await db.BusinessHours.AsNoTracking()
            .Where(h => h.DayOfWeek == date.DayOfWeek)
            .ToListAsync(ct);

        return AvailabilityCalculator.Merge(
            hours.Select(h => new TimeRange(clock.ToUtc(date, h.StartTime), clock.ToUtc(date, h.EndTime))));
    }

    /// <summary>Active appointments and time-offs that intersect the window, grouped by professional.</summary>
    public async Task<Dictionary<int, List<TimeRange>>> GetBusyRangesAsync(
        IReadOnlyCollection<int> professionalIds, TimeRange window, CancellationToken ct, int? ignoreAppointmentId = null)
    {
        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => professionalIds.Contains(a.ProfessionalId)
                        && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed)
                        && a.StartAt < window.End && a.EndAt > window.Start
                        && (ignoreAppointmentId == null || a.Id != ignoreAppointmentId))
            .Select(a => new { a.ProfessionalId, a.StartAt, a.EndAt })
            .ToListAsync(ct);

        var timeOffs = await db.ProfessionalTimeOffs.AsNoTracking()
            .Where(t => professionalIds.Contains(t.ProfessionalId) && t.StartAt < window.End && t.EndAt > window.Start)
            .Select(t => new { t.ProfessionalId, t.StartAt, t.EndAt })
            .ToListAsync(ct);

        var result = professionalIds.Distinct().ToDictionary(id => id, _ => new List<TimeRange>());
        foreach (var a in appointments) result[a.ProfessionalId].Add(new TimeRange(a.StartAt, a.EndAt));
        foreach (var t in timeOffs) result[t.ProfessionalId].Add(new TimeRange(t.StartAt, t.EndAt));
        return result;
    }

    /// <summary>Ids of active professionals that perform every one of the given services.</summary>
    public async Task<List<Professional>> GetEligibleProfessionalsAsync(
        IReadOnlyCollection<int> serviceIds, int? professionalId, CancellationToken ct)
    {
        var count = serviceIds.Count;
        return await db.Professionals.AsNoTracking()
            .Where(p => p.IsActive && (professionalId == null || p.Id == professionalId))
            .Where(p => p.Services.Count(s => serviceIds.Contains(s.ServiceId)) == count)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }
}
