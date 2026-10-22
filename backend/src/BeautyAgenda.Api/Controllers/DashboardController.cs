using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeautyAgenda.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Policies.StaffOrAdmin)]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    public Task<DashboardSummaryDto> Summary(CancellationToken ct) => dashboard.GetSummaryAsync(ct);

    /// <summary>Appointments in a period (max 62 days), for the calendar view.</summary>
    [HttpGet("appointments")]
    public Task<IReadOnlyList<CalendarAppointmentDto>> Appointments(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] int? professionalId,
        [FromQuery] bool includeCancelled,
        CancellationToken ct) =>
        dashboard.GetCalendarAsync(from, to, professionalId, includeCancelled, ct);
}
