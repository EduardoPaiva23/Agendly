using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BeautyAgenda.Application.Catalog;

public sealed record ServiceDto(int Id, string Name, string? Description, int DurationMinutes, decimal Price, bool IsActive);

public sealed record ServiceRequest(string Name, string? Description, int DurationMinutes, decimal Price, bool IsActive = true);

public sealed record PublicServiceDto(int Id, string Name, string? Description, int DurationMinutes, decimal Price);

public sealed class ServiceRequestValidator : AbstractValidator<ServiceRequest>
{
    public ServiceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(5, 480)
            .Must(m => m % 5 == 0).WithMessage("A duração deve ser múltipla de 5 minutos.");
        RuleFor(x => x.Price).InclusiveBetween(0m, 99_999.99m).PrecisionScale(10, 2, ignoreTrailingZeros: true);
    }
}

public sealed class CatalogService(IApplicationDbContext db, IValidator<ServiceRequest> validator, AuditWriter audit)
{
    public async Task<IReadOnlyList<ServiceDto>> ListAsync(bool includeInactive, CancellationToken ct) =>
        await db.Services.AsNoTracking()
            .Where(s => includeInactive || s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new ServiceDto(s.Id, s.Name, s.Description, s.DurationMinutes, s.Price, s.IsActive))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PublicServiceDto>> ListPublicAsync(CancellationToken ct) =>
        await db.Services.AsNoTracking()
            .Where(s => s.IsActive && s.Professionals.Any(ps => ps.Professional.IsActive))
            .OrderBy(s => s.Name)
            .Select(s => new PublicServiceDto(s.Id, s.Name, s.Description, s.DurationMinutes, s.Price))
            .ToListAsync(ct);

    public async Task<ServiceDto> GetAsync(int id, CancellationToken ct)
    {
        var s = await db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Service", id);
        return ToDto(s);
    }

    public async Task<ServiceDto> CreateAsync(ServiceRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureNameIsFreeAsync(request.Name.Trim(), null, ct);

        var service = new Service();
        Apply(service, request);
        db.Services.Add(service);
        await db.SaveChangesAsync(ct);

        audit.Add(nameof(Service), service.Id, "Created", request);
        await db.SaveChangesAsync(ct);
        return ToDto(service);
    }

    /// <summary>Price/duration changes affect only future bookings; existing appointments keep their snapshot.</summary>
    public async Task<ServiceDto> UpdateAsync(int id, ServiceRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct)
                      ?? throw new NotFoundException("Service", id);
        await EnsureNameIsFreeAsync(request.Name.Trim(), id, ct);

        var before = ToDto(service);
        Apply(service, request);
        audit.Add(nameof(Service), id, "Updated", new { before, after = request });
        await db.SaveChangesAsync(ct);
        return ToDto(service);
    }

    private async Task EnsureNameIsFreeAsync(string name, int? exceptId, CancellationToken ct)
    {
        if (await db.Services.AnyAsync(s => s.Name == name && s.Id != exceptId, ct))
            throw new ConflictException($"Já existe um serviço chamado '{name}'.");
    }

    private static void Apply(Service service, ServiceRequest request)
    {
        service.Name = request.Name.Trim();
        service.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        service.DurationMinutes = request.DurationMinutes;
        service.Price = request.Price;
        service.IsActive = request.IsActive;
    }

    private static ServiceDto ToDto(Service s) => new(s.Id, s.Name, s.Description, s.DurationMinutes, s.Price, s.IsActive);
}
