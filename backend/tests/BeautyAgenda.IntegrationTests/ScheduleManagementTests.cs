using System.Net;
using System.Net.Http.Json;
using BeautyAgenda.Application.Appointments;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Dashboard;
using BeautyAgenda.Application.Professionals;
using BeautyAgenda.Application.TimeOffs;
using BeautyAgenda.Domain.Entities;
using BeautyAgenda.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeautyAgenda.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ScheduleManagementTests(ApiFactory factory)
{
    private static readonly DateOnly Wednesday = Scenario.Tuesday.AddDays(1);

    [Fact]
    public async Task Time_off_removes_slots_and_deleting_it_restores_them()
    {
        var s = await Scenario.CreateAsync(factory);
        var request = new CreateTimeOffRequest(Scenario.At(Wednesday, 12), Scenario.At(Wednesday, 13), "Almoço");

        var created = await s.Admin.PostAsJsonAsync($"/api/professionals/{s.Professional.Id}/time-offs", request, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var timeOff = (await created.Content.ReadFromJsonAsync<TimeOffDto>(ApiFactory.Json))!;

        var slots = await s.SlotStartsAsync(Wednesday);
        Assert.DoesNotContain(Scenario.At(Wednesday, 11, 30), slots); // 11:30-12:30 crosses the time-off
        Assert.DoesNotContain(Scenario.At(Wednesday, 12, 15), slots);
        Assert.Contains(Scenario.At(Wednesday, 11), slots);           // 11:00-12:00 adjacent
        Assert.Contains(Scenario.At(Wednesday, 13), slots);

        var delete = await s.Admin.DeleteAsync($"/api/professionals/{s.Professional.Id}/time-offs/{timeOff.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Contains(Scenario.At(Wednesday, 12), await s.SlotStartsAsync(Wednesday));
    }

    [Fact]
    public async Task Time_off_over_an_active_appointment_is_rejected()
    {
        var s = await Scenario.CreateAsync(factory);
        await s.BookOkAsync(Scenario.At(Wednesday, 15));

        var response = await s.Admin.PostAsJsonAsync($"/api/professionals/{s.Professional.Id}/time-offs",
            new CreateTimeOffRequest(Scenario.At(Wednesday, 14), Scenario.At(Wednesday, 18), null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var invalid = await s.Admin.PostAsJsonAsync($"/api/professionals/{s.Professional.Id}/time-offs",
            new CreateTimeOffRequest(Scenario.At(Wednesday, 14), Scenario.At(Wednesday, 13), null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Inactive_or_unqualified_professionals_are_not_offered()
    {
        var s = await Scenario.CreateAsync(factory);
        Assert.NotEmpty((await s.AvailabilityAsync(Wednesday)).Professionals);

        var deactivate = await s.Admin.PutAsJsonAsync($"/api/professionals/{s.Professional.Id}",
            new ProfessionalRequest(s.Professional.Name, null, IsActive: false), ApiFactory.Json);
        deactivate.EnsureSuccessStatusCode();
        Assert.Empty((await s.AvailabilityAsync(Wednesday)).Professionals);

        var other = await Scenario.CreateAsync(factory);
        var response = await other.Anonymous.GetFromJsonAsync<BeautyAgenda.Application.Availability.AvailabilityResponse>(
            $"/api/public/availability?date={Wednesday:yyyy-MM-dd}&serviceIds={other.Service.Id}&serviceIds={s.Service.Id}", ApiFactory.Json);
        Assert.Empty(response!.Professionals); // nobody performs both services
    }

    [Fact]
    public async Task Status_transitions_are_enforced()
    {
        var s = await Scenario.CreateAsync(factory);
        var booking = await s.BookOkAsync(Scenario.At(Wednesday, 9));
        var id = await s.AppointmentIdAsync(booking.Reference);

        var confirm = await s.Admin.PatchAsJsonAsync($"/api/appointments/{id}/status",
            new ChangeStatusRequest(AppointmentStatus.Confirmed, null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var complete = await s.Admin.PatchAsJsonAsync($"/api/appointments/{id}/status",
            new ChangeStatusRequest(AppointmentStatus.Completed, null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        var back = await s.Admin.PatchAsJsonAsync($"/api/appointments/{id}/status",
            new ChangeStatusRequest(AppointmentStatus.Scheduled, null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, back.StatusCode);

        var cancel = await s.Admin.PostAsync($"/api/appointments/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);

        var audit = await factory.WithDbAsync(db => db.AdminAuditLogs
            .CountAsync(l => l.EntityName == nameof(Appointment) && l.EntityId == id.ToString()));
        Assert.Equal(2, audit);
    }

    [Fact]
    public async Task Reschedule_into_conflict_fails_and_leaves_appointment_unchanged()
    {
        var s = await Scenario.CreateAsync(factory);
        await s.BookOkAsync(Scenario.At(Wednesday, 10));
        var second = await s.BookOkAsync(Scenario.At(Wednesday, 14));
        var id = await s.AppointmentIdAsync(second.Reference);

        var conflict = await s.Admin.PostAsJsonAsync($"/api/appointments/{id}/reschedule",
            new RescheduleRequest(Scenario.At(Wednesday, 10, 30), null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var outside = await s.Admin.PostAsJsonAsync($"/api/appointments/{id}/reschedule",
            new RescheduleRequest(Scenario.At(Wednesday, 17, 30), null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, outside.StatusCode);

        var unchanged = await s.Admin.GetFromJsonAsync<AppointmentDto>($"/api/appointments/{id}", ApiFactory.Json);
        Assert.Equal(Scenario.At(Wednesday, 14), unchanged!.StartAt);
    }

    [Fact]
    public async Task Reschedule_moves_appointment_keeps_price_and_frees_old_slot()
    {
        var s = await Scenario.CreateAsync(factory);
        var booking = await s.BookOkAsync(Scenario.At(Wednesday, 11), s.Service.Id, s.SecondService.Id);
        var id = await s.AppointmentIdAsync(booking.Reference);

        // Overlapping its own current interval is fine: the appointment itself is ignored.
        var moved = await s.Admin.PostAsJsonAsync($"/api/appointments/{id}/reschedule",
            new RescheduleRequest(Scenario.At(Wednesday, 11, 30), null), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var dto = (await moved.Content.ReadFromJsonAsync<AppointmentDto>(ApiFactory.Json))!;

        Assert.Equal(Scenario.At(Wednesday, 11, 30), dto.StartAt);
        Assert.Equal(Scenario.At(Wednesday, 13), dto.EndAt);
        Assert.Equal(150m, dto.TotalPrice);
        // 11:00-11:30 was occupied by the old interval and is free again.
        Assert.Contains(Scenario.At(Wednesday, 11), await s.SlotStartsAsync(Wednesday, s.SecondService.Id));
    }

    [Fact]
    public async Task Reschedule_to_unqualified_professional_is_rejected()
    {
        var s = await Scenario.CreateAsync(factory);
        var other = await Scenario.CreateAsync(factory);
        var booking = await s.BookOkAsync(Scenario.At(Wednesday, 16));
        var id = await s.AppointmentIdAsync(booking.Reference);

        var response = await s.Admin.PostAsJsonAsync($"/api/appointments/{id}/reschedule",
            new RescheduleRequest(Scenario.At(Wednesday, 16), other.Professional.Id), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_cancel_preserves_history_and_lists_with_filters()
    {
        var s = await Scenario.CreateAsync(factory);
        var booking = await s.BookOkAsync(Scenario.At(Wednesday, 17));
        var id = await s.AppointmentIdAsync(booking.Reference);

        var cancel = await s.Admin.PostAsJsonAsync($"/api/appointments/{id}/cancel",
            new CancelAppointmentRequest("Profissional doente"), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var list = await s.Admin.GetFromJsonAsync<PagedResult<AppointmentDto>>(
            $"/api/appointments?professionalId={s.Professional.Id}&status=Cancelled", ApiFactory.Json);
        var item = Assert.Single(list!.Items);
        Assert.Equal("Profissional doente", item.CancellationReason);
        Assert.NotNull(item.CancelledAt);
        Assert.Equal(100m, item.TotalPrice);

        var calendar = await s.Admin.GetFromJsonAsync<List<CalendarAppointmentDto>>(
            $"/api/dashboard/appointments?from={Uri.EscapeDataString(Scenario.At(Wednesday, 0).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(Scenario.At(Wednesday.AddDays(1), 0).ToString("O"))}&professionalId={s.Professional.Id}",
            ApiFactory.Json);
        Assert.Empty(calendar!); // cancelled ones are hidden by default
    }

    [Fact]
    public async Task Missing_appointment_returns_404()
    {
        var admin = await factory.CreateClientAsAsync(ApiFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/appointments/999999")).StatusCode);
    }
}
