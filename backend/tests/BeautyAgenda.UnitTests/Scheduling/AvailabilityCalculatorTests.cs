using BeautyAgenda.Domain.Scheduling;

namespace BeautyAgenda.UnitTests.Scheduling;

public class AvailabilityCalculatorTests
{
    private static readonly DateTime Day = new(2030, 3, 4, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);
    private static readonly DateTime NoMin = DateTime.MinValue;
    private static readonly DateTime NoMax = DateTime.MaxValue;

    private static DateTime At(int hour, int minute = 0) => Day.AddHours(hour).AddMinutes(minute);
    private static TimeRange Range(int h1, int m1, int h2, int m2) => new(At(h1, m1), At(h2, m2));
    private static TimeSpan Minutes(int m) => TimeSpan.FromMinutes(m);

    private static IReadOnlyList<TimeRange> Slots(
        IEnumerable<TimeRange> working, IEnumerable<TimeRange>? busy, int durationMinutes,
        DateTime? earliest = null, DateTime? latest = null) =>
        AvailabilityCalculator.GetAvailableSlots(working, busy ?? [], Minutes(durationMinutes), Step,
            earliest ?? NoMin, latest ?? NoMax);

    [Fact]
    public void Last_slot_ends_exactly_at_closing_time()
    {
        var slots = Slots([Range(9, 0, 10, 0)], null, 30);

        Assert.Equal([At(9, 0), At(9, 15), At(9, 30)], slots.Select(s => s.Start));
        Assert.Equal(At(10, 0), slots[^1].End);
    }

    [Fact]
    public void Service_longer_than_working_interval_has_no_slots()
    {
        Assert.Empty(Slots([Range(9, 0, 10, 0)], null, 75));
    }

    [Fact]
    public void Day_without_business_hours_has_no_slots()
    {
        Assert.Empty(Slots([], null, 30));
    }

    [Fact]
    public void Slot_cannot_span_a_gap_between_intervals()
    {
        // 09-12 and 13-18 (lunch break): a 60 min service cannot start at 11:30.
        var slots = Slots([Range(9, 0, 12, 0), Range(13, 0, 18, 0)], null, 60);

        Assert.Contains(slots, s => s.Start == At(11, 0));
        Assert.DoesNotContain(slots, s => s.Start == At(11, 15));
        Assert.DoesNotContain(slots, s => s.Start == At(12, 0));
        Assert.Contains(slots, s => s.Start == At(13, 0));
    }

    [Fact]
    public void Touching_intervals_are_merged_so_a_service_may_cross_the_boundary()
    {
        var slots = Slots([Range(9, 0, 12, 0), Range(12, 0, 14, 0)], null, 60);

        Assert.Contains(slots, s => s.Start == At(11, 30));
    }

    [Fact]
    public void Existing_appointment_blocks_overlapping_slots_but_not_adjacent_ones()
    {
        var busy = new[] { Range(10, 0, 11, 0) };
        var slots = Slots([Range(9, 0, 12, 0)], busy, 30);

        Assert.Contains(slots, s => s.Start == At(9, 30));      // ends 10:00 == busy start: adjacent, allowed
        Assert.DoesNotContain(slots, s => s.Start == At(9, 45)); // ends 10:15: overlaps
        Assert.DoesNotContain(slots, s => s.Start == At(10, 30));
        Assert.Contains(slots, s => s.Start == At(11, 0));      // starts at busy end: adjacent, allowed
    }

    [Fact]
    public void Partial_time_off_blocks_only_its_period()
    {
        var timeOff = new[] { Range(14, 10, 14, 20) };
        var slots = Slots([Range(14, 0, 15, 0)], timeOff, 15);

        Assert.Equal([At(14, 30), At(14, 45)], slots.Select(s => s.Start));
    }

    [Fact]
    public void Total_duration_of_multiple_services_reduces_available_starts()
    {
        var single = Slots([Range(9, 0, 11, 0)], null, 30);
        var combined = Slots([Range(9, 0, 11, 0)], null, 30 + 60);

        Assert.Equal(7, single.Count);   // 09:00 .. 10:30
        Assert.Equal(3, combined.Count); // 09:00 .. 09:30
        Assert.All(combined, s => Assert.Equal(Minutes(90), s.Duration));
    }

    [Fact]
    public void Minimum_advance_and_maximum_horizon_are_respected()
    {
        var slots = Slots([Range(9, 0, 12, 0)], null, 30, earliest: At(10, 5), latest: At(11, 0));

        Assert.Equal([At(10, 15), At(10, 30), At(10, 45), At(11, 0)], slots.Select(s => s.Start));
    }

    [Fact]
    public void IsAvailable_accepts_unaligned_start_within_hours_and_free()
    {
        var candidate = new TimeRange(At(9, 7), At(9, 37));
        Assert.True(AvailabilityCalculator.IsAvailable(candidate, [Range(9, 0, 12, 0)], [], NoMin, NoMax));
    }

    [Theory]
    [InlineData(8, 45, 9, 15)]   // starts before opening
    [InlineData(11, 45, 12, 15)] // ends after closing
    [InlineData(10, 30, 11, 30)] // overlaps busy
    public void IsAvailable_rejects_invalid_candidates(int h1, int m1, int h2, int m2)
    {
        var candidate = new TimeRange(At(h1, m1), At(h2, m2));
        Assert.False(AvailabilityCalculator.IsAvailable(candidate, [Range(9, 0, 12, 0)], [Range(11, 0, 11, 15)], NoMin, NoMax));
    }

    [Fact]
    public void IsAvailable_rejects_start_before_policy_window()
    {
        var candidate = new TimeRange(At(9, 0), At(9, 30));
        Assert.False(AvailabilityCalculator.IsAvailable(candidate, [Range(9, 0, 12, 0)], [], At(9, 1), NoMax));
    }
}
