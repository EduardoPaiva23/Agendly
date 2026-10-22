namespace BeautyAgenda.Domain.Scheduling;

/// <summary>
/// Pure slot computation. All instants must share the same clock (UTC in this app).
/// </summary>
public static class AvailabilityCalculator
{
    /// <param name="workingRanges">Intervals when the professional may work (salon hours for the day).</param>
    /// <param name="busyRanges">Active appointments and time-offs.</param>
    /// <param name="duration">Total duration of the requested services.</param>
    /// <param name="step">Distance between candidate start times, anchored at each working range start.</param>
    /// <param name="earliestStart">Booking policy: minimum start instant (inclusive).</param>
    /// <param name="latestStart">Booking policy: maximum start instant (inclusive).</param>
    public static IReadOnlyList<TimeRange> GetAvailableSlots(
        IEnumerable<TimeRange> workingRanges,
        IEnumerable<TimeRange> busyRanges,
        TimeSpan duration,
        TimeSpan step,
        DateTime earliestStart,
        DateTime latestStart)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (step <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(step));

        var busy = busyRanges.ToList();
        var slots = new List<TimeRange>();

        foreach (var working in Merge(workingRanges))
        {
            for (var start = working.Start; start + duration <= working.End; start += step)
            {
                if (start < earliestStart || start > latestStart) continue;

                var candidate = new TimeRange(start, start + duration);
                if (!busy.Any(b => b.Overlaps(candidate)))
                    slots.Add(candidate);
            }
        }

        return slots;
    }

    /// <summary>Checks an arbitrary interval (not necessarily aligned to the slot grid).</summary>
    public static bool IsAvailable(
        TimeRange candidate,
        IEnumerable<TimeRange> workingRanges,
        IEnumerable<TimeRange> busyRanges,
        DateTime earliestStart,
        DateTime latestStart)
    {
        if (candidate.Start < earliestStart || candidate.Start > latestStart) return false;
        if (!Merge(workingRanges).Any(w => w.Contains(candidate))) return false;
        return !busyRanges.Any(b => b.Overlaps(candidate));
    }

    /// <summary>Joins overlapping or touching ranges so a service may span e.g. 09:00-12:00 + 12:00-18:00.</summary>
    public static IReadOnlyList<TimeRange> Merge(IEnumerable<TimeRange> ranges)
    {
        var ordered = ranges.OrderBy(r => r.Start).ToList();
        var merged = new List<TimeRange>();
        foreach (var range in ordered)
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End)
            {
                var last = merged[^1];
                merged[^1] = new TimeRange(last.Start, range.End > last.End ? range.End : last.End);
            }
            else
            {
                merged.Add(range);
            }
        }
        return merged;
    }
}
