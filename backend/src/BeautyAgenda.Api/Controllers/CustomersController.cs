using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeautyAgenda.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize(Policy = Policies.StaffOrAdmin)]
public sealed class CustomersController(CustomersService customers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CustomerDto>> Search(
        [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        customers.SearchAsync(search, page, pageSize, ct);

    [HttpGet("{id:int}")]
    public Task<CustomerDto> Get(int id, CancellationToken ct) => customers.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CustomerRequest request, CancellationToken ct)
    {
        var created = await customers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<CustomerDto> Update(int id, CustomerRequest request, CancellationToken ct) =>
        customers.UpdateAsync(id, request, ct);
}
