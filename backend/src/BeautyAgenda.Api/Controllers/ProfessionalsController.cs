using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Professionals;
using BeautyAgenda.Application.TimeOffs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeautyAgenda.Api.Controllers;

[ApiController]
[Route("api/professionals")]
[Authorize(Policy = Policies.StaffOrAdmin)]
public sealed class ProfessionalsController(ProfessionalsService professionals, TimeOffsService timeOffs) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProfessionalDto>> List([FromQuery] bool includeInactive = true, CancellationToken ct = default) =>
        professionals.ListAsync(includeInactive, ct);

    [HttpGet("{id:int}")]
    public Task<ProfessionalDto> Get(int id, CancellationToken ct) => professionals.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<ProfessionalDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(ProfessionalRequest request, CancellationToken ct)
    {
        var created = await professionals.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<ProfessionalDto> Update(int id, ProfessionalRequest request, CancellationToken ct) =>
        professionals.UpdateAsync(id, request, ct);

    [HttpPut("{id:int}/services")]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<ProfessionalDto> SetServices(int id, SetProfessionalServicesRequest request, CancellationToken ct) =>
        professionals.SetServicesAsync(id, request, ct);

    [HttpGet("{id:int}/time-offs")]
    public Task<IReadOnlyList<TimeOffDto>> ListTimeOffs(
        int id, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct) =>
        timeOffs.ListAsync(id, from, to, ct);

    [HttpPost("{id:int}/time-offs")]
    [ProducesResponseType<TimeOffDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTimeOff(int id, CreateTimeOffRequest request, CancellationToken ct)
    {
        var created = await timeOffs.CreateAsync(id, request, ct);
        return Created($"/api/professionals/{id}/time-offs/{created.Id}", created);
    }

    [HttpDelete("{id:int}/time-offs/{timeOffId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTimeOff(int id, int timeOffId, CancellationToken ct)
    {
        await timeOffs.DeleteAsync(id, timeOffId, ct);
        return NoContent();
    }
}
