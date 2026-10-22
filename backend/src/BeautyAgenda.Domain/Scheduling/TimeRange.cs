namespace BeautyAgenda.Domain.Scheduling;

/// <summary>Half-open interval [Start, End). Adjacent ranges (a.End == b.Start) do not overlap.</summary>
public readonly record struct TimeRange
{
    public TimeRange(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new ArgumentException("End must be after start.", nameof(end));
        Start = start;
        End = end;
    }

    public DateTime Start { get; }
    public DateTime End { get; }
    public TimeSpan Duration => End - Start;

    public bool Overlaps(TimeRange other) => Start < other.End && End > other.Start;

    public bool Contains(TimeRange other) => other.Start >= Start && other.End <= End;
}
