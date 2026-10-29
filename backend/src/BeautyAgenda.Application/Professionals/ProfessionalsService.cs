using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BeautyAgenda.Application.Professionals;

public sealed record ProfessionalDto(int Id, string Name, string? Phone, bool IsActive, IReadOnlyList<int> ServiceIds);

public sealed record ProfessionalRequest(string Name, string? Phone, bool IsActive = true);

public sealed record SetProfessionalServicesRequest(IReadOnlyList<int> ServiceIds);

/// <summary>Public view: no contact data.</summary>
public sealed record PublicProfessionalDto(int Id, string Name, IReadOnlyList<int> ServiceIds);

public sealed class ProfessionalRequestValidator : AbstractValidator<ProfessionalRequest>
{
    public ProfessionalRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Phone).Must(PhoneNumber.IsValid).When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Informe o telefone com DDD ou deixe em branco.");
    }
}

public sealed class SetProfessionalServicesRequestValidator : AbstractValidator<SetProfessionalServicesRequest>
{
    public SetProfessionalServicesRequestValidator()
    {
        RuleFor(x => x.ServiceIds).NotNull()
            .Must(ids => ids.Count <= 100).WithMessage("Serviços demais.")
            .Must(ids => ids.All(id => id > 0)).WithMessage("Os serviços informados são inválidos.");
    }
}

public sealed class ProfessionalsService(
    IApplicationDbContext db,
    IValidator<ProfessionalRequest> validator,
    IValidator<SetProfessionalServicesRequest> servicesValidator,
    AuditWriter audit)
{
    public async Task<IReadOnlyList<ProfessionalDto>> ListAsync(bool includeInactive, CancellationToken ct) =>
        await db.Professionals.AsNoTracking()
            .Where(p => includeInactive || p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProfessionalDto(p.Id, p.Name, p.Phone, p.IsActive,
                p.Services.OrderBy(s => s.ServiceId).Select(s => s.ServiceId).ToList()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PublicProfessionalDto>> ListPublicAsync(CancellationToken ct) =>
        await db.Professionals.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new PublicProfessionalDto(p.Id, p.Name,
                p.Services.Where(s => s.Service.IsActive).OrderBy(s => s.ServiceId).Select(s => s.ServiceId).ToList()))
            .ToListAsync(ct);

    public async Task<ProfessionalDto> GetAsync(int id, CancellationToken ct) =>
        await db.Professionals.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProfessionalDto(p.Id, p.Name, p.Phone, p.IsActive,
                p.Services.OrderBy(s => s.ServiceId).Select(s => s.ServiceId).ToList()))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Professional", id);

    public async Task<ProfessionalDto> CreateAsync(ProfessionalRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var professional = new Professional();
        Apply(professional, request);
        db.Professionals.Add(professional);
        await db.SaveChangesAsync(ct);

        audit.Add(nameof(Professional), professional.Id, "Created");
        await db.SaveChangesAsync(ct);
        return await GetAsync(professional.Id, ct);
    }

    /// <summary>Deactivating hides the professional from new bookings; existing appointments are kept.</summary>
    public async Task<ProfessionalDto> UpdateAsync(int id, ProfessionalRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var professional = await db.Professionals.FirstOrDefaultAsync(p => p.Id == id, ct)
                           ?? throw new NotFoundException("Professional", id);
        Apply(professional, request);
        audit.Add(nameof(Professional), id, "Updated", new { request.Name, request.IsActive });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ProfessionalDto> SetServicesAsync(int id, SetProfessionalServicesRequest request, CancellationToken ct)
    {
        await servicesValidator.ValidateAndThrowAsync(request, ct);
        var professional = await db.Professionals.Include(p => p.Services).FirstOrDefaultAsync(p => p.Id == id, ct)
                           ?? throw new NotFoundException("Professional", id);

        var wanted = request.ServiceIds.Distinct().ToHashSet();
        var existing = await db.Services.Where(s => wanted.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
        var unknown = wanted.Except(existing).ToList();
        if (unknown.Count > 0)
            throw Validation.Error(nameof(request.ServiceIds), $"Serviços inexistentes: {string.Join(", ", unknown)}.");

        foreach (var link in professional.Services.Where(s => !wanted.Contains(s.ServiceId)).ToList())
            professional.Services.Remove(link);
        foreach (var serviceId in wanted.Where(w => professional.Services.All(s => s.ServiceId != w)))
            professional.Services.Add(new ProfessionalService { ProfessionalId = id, ServiceId = serviceId });

        audit.Add(nameof(Professional), id, "ServicesChanged", new { serviceIds = wanted.Order() });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static void Apply(Professional professional, ProfessionalRequest request)
    {
        professional.Name = request.Name.Trim();
        professional.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : PhoneNumber.Normalize(request.Phone);
        professional.IsActive = request.IsActive;
    }
}
