using BeautyAgenda.Application.Appointments;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeautyAgenda.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize(Policy = Policies.StaffOrAdmin)]
public sealed class AppointmentsController(AppointmentsService appointments) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AppointmentDto>> List(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? professionalId,
        [FromQuery] int? customerId,
        [FromQuery] AppointmentStatus? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct) =>
        appointments.ListAsync(new AppointmentListQuery(from, to, professionalId, customerId, status, page, pageSize), ct);

    [HttpGet("{id:int}")]
    public Task<AppointmentDto> Get(int id, CancellationToken ct) => appointments.GetAsync(id, ct);

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<AppointmentDto> ChangeStatus(int id, ChangeStatusRequest request, CancellationToken ct) =>
        appointments.ChangeStatusAsync(id, request, ct);

    [HttpPost("{id:int}/reschedule")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<AppointmentDto> Reschedule(int id, RescheduleRequest request, CancellationToken ct) =>
        appointments.RescheduleAsync(id, request, ct);

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<AppointmentDto> Cancel(int id, CancelAppointmentRequest? request, CancellationToken ct) =>
        appointments.CancelAsync(id, request ?? new CancelAppointmentRequest(null), ct);
}
