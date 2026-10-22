using BeautyAgenda.Application.Appointments;
using BeautyAgenda.Application.Availability;
using BeautyAgenda.Application.BusinessHours;
using BeautyAgenda.Application.Catalog;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Customers;
using BeautyAgenda.Application.Dashboard;
using BeautyAgenda.Application.Professionals;
using BeautyAgenda.Application.Scheduling;
using BeautyAgenda.Application.TimeOffs;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BeautyAgenda.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SalonOptions>().Bind(configuration.GetSection(SalonOptions.Section))
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Salon:TimeZone is not a valid time zone id.")
            .ValidateOnStart();
        services.AddOptions<SchedulingOptions>().Bind(configuration.GetSection(SchedulingOptions.Section))
            .Validate(o => o.SlotIntervalMinutes is >= 5 and <= 120 && o.MinAdvanceMinutes >= 0
                           && o.MaxAdvanceDays is >= 1 and <= 365 && o.PublicCancellationMinNoticeHours >= 0
                           && o.MaxServicesPerAppointment is >= 1 and <= 20,
                "Scheduling options are out of range.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SalonClock>();
        services.AddScoped<ScheduleReader>();
        services.AddScoped<AuditWriter>();

        services.AddScoped<CustomersService>();
        services.AddScoped<ProfessionalsService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<BusinessHoursService>();
        services.AddScoped<TimeOffsService>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<PublicBookingService>();
        services.AddScoped<AppointmentsService>();
        services.AddScoped<DashboardService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Singleton);
        return services;
    }
}
