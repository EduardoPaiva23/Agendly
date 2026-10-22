using BeautyAgenda.Domain.Scheduling;

namespace BeautyAgenda.UnitTests.Scheduling;

public class TimeRangeTests
{
    private static readonly DateTime T0 = new(2030, 3, 4, 12, 0, 0, DateTimeKind.Utc);
    private static TimeRange R(int startMin, int endMin) => new(T0.AddMinutes(startMin), T0.AddMinutes(endMin));

    [Theory]
    [InlineData(0, 60, 30, 90, true)]   // partial overlap
    [InlineData(0, 60, 10, 20, true)]   // contained
    [InlineData(10, 20, 0, 60, true)]   // contains
    [InlineData(0, 60, 0, 60, true)]    // identical
    [InlineData(0, 60, 60, 90, false)]  // adjacent after
    [InlineData(60, 90, 0, 60, false)]  // adjacent before
    [InlineData(0, 30, 45, 90, false)]  // disjoint
    public void Overlap_follows_NewStart_lt_ExistingEnd_and_NewEnd_gt_ExistingStart(
        int aStart, int aEnd, int bStart, int bEnd, bool expected)
    {
        Assert.Equal(expected, R(aStart, aEnd).Overlaps(R(bStart, bEnd)));
        Assert.Equal(expected, R(bStart, bEnd).Overlaps(R(aStart, aEnd)));
    }

    [Fact]
    public void Empty_or_inverted_range_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => R(10, 10));
        Assert.Throws<ArgumentException>(() => R(10, 5));
    }

    [Fact]
    public void Merge_joins_overlapping_and_touching_ranges()
    {
        var merged = AvailabilityCalculator.Merge([R(60, 120), R(0, 30), R(30, 45), R(100, 150)]);
        Assert.Equal([R(0, 45), R(60, 150)], merged);
    }
}
