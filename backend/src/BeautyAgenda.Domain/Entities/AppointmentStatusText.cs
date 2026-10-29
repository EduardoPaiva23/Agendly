namespace BeautyAgenda.Domain.Entities;

/// <summary>Status names as shown to users (pt-BR), used in error messages.</summary>
public static class AppointmentStatusText
{
    public static string Of(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Scheduled => "agendado",
        AppointmentStatus.Confirmed => "confirmado",
        AppointmentStatus.Completed => "concluído",
        AppointmentStatus.Cancelled => "cancelado",
        _ => status.ToString(),
    };
}
