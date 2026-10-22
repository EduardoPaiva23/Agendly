using BeautyAgenda.Application.BusinessHours;
using BeautyAgenda.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeautyAgenda.Api.Controllers;

[ApiController]
[Route("api/business-hours")]
[Authorize(Policy = Policies.StaffOrAdmin)]
public sealed class BusinessHoursController(BusinessHoursService businessHours) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<BusinessHourDto>> Get(CancellationToken ct) => businessHours.GetAsync(ct);

    [HttpPut]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<IReadOnlyList<BusinessHourDto>> Replace(UpdateBusinessHoursRequest request, CancellationToken ct) =>
        businessHours.ReplaceAsync(request, ct);
}
