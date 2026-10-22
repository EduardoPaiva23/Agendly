using System.Net;
using System.Net.Http.Json;
using BeautyAgenda.Application.Appointments;
using BeautyAgenda.Application.Catalog;
using BeautyAgenda.Domain.Entities;
using BeautyAgenda.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeautyAgenda.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BookingTests(ApiFactory factory)
{
    [Fact]
    public async Task Public_booking_creates_appointment_and_removes_the_slot()
    {
        var s = await Scenario.CreateAsync(factory);
        var start = Scenario.At(Scenario.Tuesday, 10);
        Assert.Contains(start, await s.SlotStartsAsync(Scenario.Tuesday));

        var booking = await s.BookOkAsync(start, s.Service.Id, s.SecondService.Id);

        Assert.Equal(AppointmentStatus.Scheduled, booking.Status);
        Assert.Equal(start, booking.StartAt);
        Assert.Equal(start.AddMinutes(90), booking.EndAt);   // 60 + 30, computed by the backend
        Assert.Equal(150m, booking.TotalPrice);
        Assert.Equal(22, booking.Reference.Length);

        var slots = await s.SlotStartsAsync(Scenario.Tuesday);
        Assert.DoesNotContain(start, slots);
        Assert.DoesNotContain(Scenario.At(Scenario.Tuesday, 11), slots);  // inside 10:00-11:30
        Assert.DoesNotContain(Scenario.At(Scenario.Tuesday, 9, 30), slots); // 9:30-10:30 overlaps
        Assert.Contains(Scenario.At(Scenario.Tuesday, 9), slots);           // 9:00-10:00 is adjacent
        Assert.Contains(Scenario.At(Scenario.Tuesday, 11, 30), slots);      // starts at the end
    }

    [Fact]
    public async Task Booking_an_occupied_slot_returns_409()
    {
        var s = await Scenario.CreateAsync(factory);
        await s.BookOkAsync(Scenario.At(Scenario.Tuesday, 14));

        var overlapping = await s.BookAsync(s.BookingRequest(Scenario.At(Scenario.Tuesday, 14, 30)));
        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);
    }

    [Fact]
    public async Task Concurrent_requests_for_the_same_slot_confirm_exactly_one()
    {
        var s = await Scenario.CreateAsync(factory);
        var start = Scenario.At(Scenario.Tuesday.AddDays(1), 15);

        var attempts = Enumerable.Range(0, 10).Select(_ => s.BookAsync(s.BookingRequest(start)));
        var responses = await Task.WhenAll(attempts);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var stored = await factory.WithDbAsync(db => db.Appointments
            .CountAsync(a => a.ProfessionalId == s.Professional.Id && a.Status == AppointmentStatus.Scheduled));
        Assert.Equal(1, stored);
    }

    [Fact]
    public async Task Concurrent_overlapping_requests_with_different_starts_confirm_only_one()
    {
        var s = await Scenario.CreateAsync(factory);
        var day = Scenario.Tuesday.AddDays(2);
        var starts = new[] { Scenario.At(day, 10), Scenario.At(day, 10, 15), Scenario.At(day, 10, 30), Scenario.At(day, 10, 45) };

        var responses = await Task.WhenAll(starts.Select(st => s.BookAsync(s.BookingRequest(st))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
    }

    [Theory]
    [InlineData(8, 0)]   // before opening
    [InlineData(17, 30)] // 60 min would end after 18:00
    [InlineData(10, 7)]  // not on the 15-minute grid
    public async Task Times_that_are_not_offered_are_rejected(int hour, int minute)
    {
        var s = await Scenario.CreateAsync(factory);
        var response = await s.BookAsync(s.BookingRequest(Scenario.At(Scenario.Tuesday, hour, minute)));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Booking_respects_minimum_advance_and_sunday_closure()
    {
        var s = await Scenario.CreateAsync(factory);
        // Now is Monday 09:00; minimum advance is 60 min.
        Assert.Equal(HttpStatusCode.Conflict, (await s.BookAsync(s.BookingRequest(Scenario.At(ApiFactory.Today, 9, 30)))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await s.BookAsync(s.BookingRequest(Scenario.At(ApiFactory.Today, 10)))).StatusCode);

        var sunday = ApiFactory.Today.AddDays(6);
        Assert.Empty(await s.SlotStartsAsync(sunday));
    }

    [Fact]
    public async Task Invalid_input_returns_400_and_client_values_are_never_trusted()
    {
        var s = await Scenario.CreateAsync(factory);
        var start = Scenario.At(Scenario.Tuesday, 9);

        var noServices = s.BookingRequest(start) with { ServiceIds = [] };
        Assert.Equal(HttpStatusCode.BadRequest, (await s.BookAsync(noServices)).StatusCode);

        var badPhone = s.BookingRequest(start) with { CustomerPhone = "123" };
        Assert.Equal(HttpStatusCode.BadRequest, (await s.BookAsync(badPhone)).StatusCode);

        var unknownService = s.BookingRequest(start) with { ServiceIds = [999999] };
        Assert.Equal(HttpStatusCode.BadRequest, (await s.BookAsync(unknownService)).StatusCode);

        var other = await Scenario.CreateAsync(factory);
        var notOffered = s.BookingRequest(start) with { ServiceIds = [other.Service.Id] };
        Assert.Equal(HttpStatusCode.BadRequest, (await s.BookAsync(notOffered)).StatusCode);
    }

    [Fact]
    public async Task Contracted_price_and_duration_survive_catalog_changes()
    {
        var s = await Scenario.CreateAsync(factory);
        var booking = await s.BookOkAsync(Scenario.At(Scenario.Tuesday, 16));

        var update = await s.Admin.PutAsJsonAsync($"/api/services/{s.Service.Id}",
            new ServiceRequest(s.Service.Name, null, 120, 999m), ApiFactory.Json);
        update.EnsureSuccessStatusCode();

        var after = await s.Anonymous.GetFromJsonAsync<PublicAppointmentDto>(
            $"/api/public/appointments/{booking.Reference}", ApiFactory.Json);
        Assert.Equal(100m, after!.TotalPrice);
        Assert.Equal(booking.EndAt, after.EndAt);
        Assert.Equal(60, after.Services.Single().DurationMinutes);
    }

    [Fact]
    public async Task Returning_customer_is_recognized_by_phone_without_overwriting_data()
    {
        var s = await Scenario.CreateAsync(factory);
        var phone = Scenario.NewPhone();

        var first = s.BookingRequest(Scenario.At(Scenario.Tuesday, 9)) with { CustomerName = "Nome Original", CustomerPhone = phone };
        var second = s.BookingRequest(Scenario.At(Scenario.Tuesday, 12)) with { CustomerName = "Nome Falso", CustomerPhone = $"({phone[..2]}) {phone[2..]}" };
        Assert.Equal(HttpStatusCode.Created, (await s.BookAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await s.BookAsync(second)).StatusCode);

        var customers = await factory.WithDbAsync(db => db.Customers.Where(c => c.Phone == phone).ToListAsync());
        var customer = Assert.Single(customers);
        Assert.Equal("Nome Original", customer.Name);
    }

    [Fact]
    public async Task Customer_can_cancel_by_reference_and_slot_is_released()
    {
        var s = await Scenario.CreateAsync(factory);
        var start = Scenario.At(Scenario.Tuesday, 13);
        var booking = await s.BookOkAsync(start);
        Assert.True(booking.CanCancelOnline);

        var cancel = await s.Anonymous.PostAsJsonAsync($"/api/public/appointments/{booking.Reference}/cancel",
            new PublicCancelRequest("Imprevisto"), ApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelled = await cancel.Content.ReadFromJsonAsync<PublicAppointmentDto>(ApiFactory.Json);
        Assert.Equal(AppointmentStatus.Cancelled, cancelled!.Status);

        Assert.Contains(start, await s.SlotStartsAsync(Scenario.Tuesday));

        var again = await s.Anonymous.PostAsync($"/api/public/appointments/{booking.Reference}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var stored = await factory.WithDbAsync(db => db.Appointments.SingleAsync(a => a.PublicReference == booking.Reference));
        Assert.Equal("Imprevisto", stored.CancellationReason);
    }

    [Fact]
    public async Task Online_cancellation_too_close_to_start_is_rejected()
    {
        var s = await Scenario.CreateAsync(factory);
        // Today 10:00 is 60 min ahead; the online cancellation notice is 2 h.
        var booking = await s.BookOkAsync(Scenario.At(ApiFactory.Today, 10, 15));
        Assert.False(booking.CanCancelOnline);

        var cancel = await s.Anonymous.PostAsync($"/api/public/appointments/{booking.Reference}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
    }
}
