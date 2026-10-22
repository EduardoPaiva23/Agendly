using BeautyAgenda.Domain.Common;
using BeautyAgenda.Domain.Entities;

namespace BeautyAgenda.UnitTests.Domain;

public class AppointmentTests
{
    private static readonly DateTime Now = new(2030, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2030, 3, 4, 13, 0, 0, DateTimeKind.Utc);

    private static Service Cut() => new() { Id = 1, Name = "Corte", DurationMinutes = 60, Price = 90m };
    private static Service Brow() => new() { Id = 2, Name = "Sobrancelha", DurationMinutes = 30, Price = 35.50m };

    private static Appointment NewAppointment(params Service[] services) =>
        Appointment.Create(new Customer { Id = 7, Name = "Ana", Phone = "11999999999" }, 3, Start,
            services.Length == 0 ? [Cut()] : services, "ref", Now);

    [Fact]
    public void Create_sums_durations_and_snapshots_prices()
    {
        var cut = Cut();
        var appointment = NewAppointment(cut, Brow());

        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(Start.AddMinutes(90), appointment.EndAt);
        Assert.Equal(125.50m, appointment.TotalPrice);

        cut.Price = 500m;
        cut.DurationMinutes = 120;
        Assert.Equal(125.50m, appointment.TotalPrice);
        Assert.Equal(90, appointment.TotalDurationMinutes);
    }

    [Fact]
    public void Create_requires_at_least_one_service()
    {
        Assert.Throws<DomainException>(() =>
            Appointment.Create(new Customer(), 1, Start, [], "ref", Now));
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Scheduled, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Scheduled, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Cancelled)]
    public void Allowed_transitions(AppointmentStatus from, AppointmentStatus to)
    {
        var appointment = InStatus(from);
        appointment.ChangeStatus(to, Now);
        Assert.Equal(to, appointment.Status);
        Assert.Equal(Now, appointment.UpdatedAt);
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Scheduled, AppointmentStatus.Scheduled)]
    public void Forbidden_transitions_throw(AppointmentStatus from, AppointmentStatus to)
    {
        var appointment = InStatus(from);
        Assert.Throws<DomainException>(() => appointment.ChangeStatus(to, Now));
        Assert.Equal(from, appointment.Status);
    }

    [Fact]
    public void Cancel_keeps_history_and_records_reason()
    {
        var appointment = NewAppointment();
        appointment.Cancel(Now, "  cliente pediu  ");

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal(Now, appointment.CancelledAt);
        Assert.Equal("cliente pediu", appointment.CancellationReason);
        Assert.Single(appointment.Services);
    }

    [Fact]
    public void Reschedule_moves_interval_and_keeps_contracted_values()
    {
        var appointment = NewAppointment(Cut(), Brow());
        var newStart = Start.AddDays(1);

        appointment.Reschedule(9, newStart, Now);

        Assert.Equal(9, appointment.ProfessionalId);
        Assert.Equal(newStart, appointment.StartAt);
        Assert.Equal(newStart.AddMinutes(90), appointment.EndAt);
        Assert.Equal(125.50m, appointment.TotalPrice);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void Reschedule_of_finished_appointment_is_rejected(AppointmentStatus status)
    {
        var appointment = InStatus(status);
        Assert.Throws<DomainException>(() => appointment.Reschedule(3, Start.AddDays(1), Now));
        Assert.Equal(Start, appointment.StartAt);
    }

    private static Appointment InStatus(AppointmentStatus status)
    {
        var appointment = NewAppointment();
        switch (status)
        {
            case AppointmentStatus.Confirmed: appointment.ChangeStatus(AppointmentStatus.Confirmed, Now); break;
            case AppointmentStatus.Completed: appointment.ChangeStatus(AppointmentStatus.Completed, Now); break;
            case AppointmentStatus.Cancelled: appointment.Cancel(Now, null); break;
        }
        return appointment;
    }
}
