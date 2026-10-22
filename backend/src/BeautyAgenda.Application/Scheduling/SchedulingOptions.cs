namespace BeautyAgenda.Application.Scheduling;

public sealed class SalonOptions
{
    public const string Section = "Salon";

    public string Name { get; set; } = "BeautyAgenda";
    /// <summary>IANA id, e.g. "America/Sao_Paulo".</summary>
    public string TimeZone { get; set; } = "America/Sao_Paulo";
}

public sealed class SchedulingOptions
{
    public const string Section = "Scheduling";

    public int SlotIntervalMinutes { get; set; } = 15;
    /// <summary>Public bookings must start at least this many minutes from now.</summary>
    public int MinAdvanceMinutes { get; set; } = 60;
    /// <summary>Bookings cannot start more than this many days ahead.</summary>
    public int MaxAdvanceDays { get; set; } = 60;
    /// <summary>Customers can cancel online up to this many hours before the start.</summary>
    public int PublicCancellationMinNoticeHours { get; set; } = 2;
    public int MaxServicesPerAppointment { get; set; } = 5;
}
