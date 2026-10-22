using System.Net;
using System.Net.Http.Json;
using BeautyAgenda.Application.Appointments;
using BeautyAgenda.Application.Availability;
using BeautyAgenda.Application.Catalog;
using BeautyAgenda.Application.Professionals;

namespace BeautyAgenda.IntegrationTests.Infrastructure;

/// <summary>Builds isolated test data (own services and professional) through the public API surface.</summary>
public sealed class Scenario(ApiFactory factory, HttpClient admin)
{
    private static int _phoneSeq = 100_000;

    public static readonly DateOnly Tuesday = ApiFactory.Today.AddDays(1);

    public ServiceDto Service { get; private set; } = null!;
    public ServiceDto SecondService { get; private set; } = null!;
    public ProfessionalDto Professional { get; private set; } = null!;

    public HttpClient Admin => admin;
    public HttpClient Anonymous { get; } = factory.CreateClient();

    public static async Task<Scenario> CreateAsync(ApiFactory factory)
    {
        var scenario = new Scenario(factory, await factory.CreateClientAsAsync(ApiFactory.AdminEmail));
        await scenario.InitializeAsync();
        return scenario;
    }

    private async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        Service = await PostAsync<ServiceDto>("/api/services", new ServiceRequest($"Corte {suffix}", null, 60, 100m));
        SecondService = await PostAsync<ServiceDto>("/api/services", new ServiceRequest($"Escova {suffix}", null, 30, 50m));
        Professional = await PostAsync<ProfessionalDto>("/api/professionals", new ProfessionalRequest($"Pro {suffix}", null));
        var response = await admin.PutAsJsonAsync($"/api/professionals/{Professional.Id}/services",
            new SetProfessionalServicesRequest([Service.Id, SecondService.Id]), ApiFactory.Json);
        response.EnsureSuccessStatusCode();
    }

    public static string NewPhone() => $"11{Interlocked.Increment(ref _phoneSeq):D9}";

    /// <summary>Local salon time (UTC-3) on the given date.</summary>
    public static DateTimeOffset At(DateOnly date, int hour, int minute = 0) =>
        new(date.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(-3));

    public CreatePublicAppointmentRequest BookingRequest(DateTimeOffset start, params int[] serviceIds) =>
        new(Professional.Id, serviceIds.Length == 0 ? [Service.Id] : serviceIds, start, "Cliente Teste", NewPhone(), null);

    public Task<HttpResponseMessage> BookAsync(CreatePublicAppointmentRequest request) =>
        Anonymous.PostAsJsonAsync("/api/public/appointments", request, ApiFactory.Json);

    public async Task<PublicAppointmentDto> BookOkAsync(DateTimeOffset start, params int[] serviceIds)
    {
        var response = await BookAsync(BookingRequest(start, serviceIds));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PublicAppointmentDto>(ApiFactory.Json))!;
    }

    public async Task<AvailabilityResponse> AvailabilityAsync(DateOnly date, params int[] serviceIds)
    {
        var ids = string.Join("&", (serviceIds.Length == 0 ? [Service.Id] : serviceIds).Select(id => $"serviceIds={id}"));
        return (await Anonymous.GetFromJsonAsync<AvailabilityResponse>(
            $"/api/public/availability?date={date:yyyy-MM-dd}&{ids}&professionalId={Professional.Id}", ApiFactory.Json))!;
    }

    public async Task<IReadOnlyList<DateTimeOffset>> SlotStartsAsync(DateOnly date, params int[] serviceIds) =>
        (await AvailabilityAsync(date, serviceIds)).Professionals.SelectMany(p => p.Slots).Select(s => s.StartAt).ToList();

    public async Task<int> AppointmentIdAsync(string reference) =>
        await factory.WithDbAsync(db => Task.FromResult(db.Appointments.Single(a => a.PublicReference == reference).Id));

    private async Task<T> PostAsync<T>(string url, object body)
    {
        var response = await admin.PostAsJsonAsync(url, body, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }
}
