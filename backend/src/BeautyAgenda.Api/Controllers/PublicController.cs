using BeautyAgenda.Api.Infrastructure;
using BeautyAgenda.Application.Appointments;
using BeautyAgenda.Application.Availability;
using BeautyAgenda.Application.Catalog;
using BeautyAgenda.Application.Professionals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BeautyAgenda.Api.Controllers;

/// <summary>Anonymous self-booking. Exposes only what a customer needs; never other customers' data.</summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public sealed class PublicController(
    CatalogService catalog,
    ProfessionalsService professionals,
    AvailabilityService availability,
    PublicBookingService booking) : ControllerBase
{
    [HttpGet("services")]
    public Task<IReadOnlyList<PublicServiceDto>> Services(CancellationToken ct) => catalog.ListPublicAsync(ct);

    [HttpGet("professionals")]
    public Task<IReadOnlyList<PublicProfessionalDto>> Professionals(CancellationToken ct) =>
        professionals.ListPublicAsync(ct);

    /// <summary>Bookable start times for a local date. Example: ?date=2026-11-03&amp;serviceIds=1&amp;serviceIds=5</summary>
    [HttpGet("availability")]
    public Task<AvailabilityResponse> Availability(
        [FromQuery] DateOnly date, [FromQuery] int[] serviceIds, [FromQuery] int? professionalId, CancellationToken ct) =>
        availability.GetAsync(new AvailabilityQuery(date, serviceIds, professionalId), ct);

    [HttpPost("appointments")]
    [EnableRateLimiting(RateLimitOptions.PublicWritePolicy)]
    [ProducesResponseType<PublicAppointmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Book(CreatePublicAppointmentRequest request, CancellationToken ct)
    {
        var created = await booking.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAppointment), new { reference = created.Reference }, created);
    }

    [HttpGet("appointments/{reference}")]
    public Task<PublicAppointmentDto> GetAppointment(string reference, CancellationToken ct) =>
        booking.GetAsync(reference, ct);

    [HttpPost("appointments/{reference}/cancel")]
    [EnableRateLimiting(RateLimitOptions.PublicWritePolicy)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<PublicAppointmentDto> Cancel(string reference, PublicCancelRequest? request, CancellationToken ct) =>
        booking.CancelAsync(reference, request ?? new PublicCancelRequest(null), ct);
}
