using BeautyAgenda.Application.Catalog;
using BeautyAgenda.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeautyAgenda.Api.Controllers;

[ApiController]
[Route("api/services")]
[Authorize(Policy = Policies.StaffOrAdmin)]
public sealed class ServicesController(CatalogService catalog) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ServiceDto>> List([FromQuery] bool includeInactive = true, CancellationToken ct = default) =>
        catalog.ListAsync(includeInactive, ct);

    [HttpGet("{id:int}")]
    public Task<ServiceDto> Get(int id, CancellationToken ct) => catalog.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<ServiceDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(ServiceRequest request, CancellationToken ct)
    {
        var created = await catalog.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<ServiceDto> Update(int id, ServiceRequest request, CancellationToken ct) =>
        catalog.UpdateAsync(id, request, ct);
}
