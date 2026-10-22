namespace BeautyAgenda.Domain.Entities;

/// <summary>A working interval of the salon on a weekday, in salon local time. A day may have several intervals.</summary>
public class BusinessHour
{
    public int Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
