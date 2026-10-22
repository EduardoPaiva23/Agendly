using BeautyAgenda.Domain.Scheduling;
using Microsoft.Extensions.Options;

namespace BeautyAgenda.Application.Scheduling;

/// <summary>Current time and conversions between UTC (storage) and the salon's local time zone (presentation).</summary>
public sealed class SalonClock(TimeProvider timeProvider, IOptions<SalonOptions> options)
{
    public TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateOnly Today => DateOnly.FromDateTime(ToLocal(UtcNow).DateTime);

    public DateTimeOffset ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), Zone);

    public DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), Zone);

    public DateOnly LocalDateOf(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc).DateTime);

    /// <summary>The whole local day as a UTC range.</summary>
    public TimeRange DayRange(DateOnly date) =>
        new(ToUtc(date, TimeOnly.MinValue), ToUtc(date.AddDays(1), TimeOnly.MinValue));
}
