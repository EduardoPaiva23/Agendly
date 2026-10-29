using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Scheduling;
using BeautyAgenda.Domain.Entities;
using BeautyAgenda.Domain.Scheduling;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeautyAgenda.Application.Availability;

public sealed record AvailabilityQuery(DateOnly Date, IReadOnlyList<int> ServiceIds, int? ProfessionalId);

public sealed record SlotDto(DateTimeOffset StartAt, DateTimeOffset EndAt);

public sealed record ProfessionalAvailabilityDto(int ProfessionalId, string ProfessionalName, IReadOnlyList<SlotDto> Slots);

public sealed record AvailabilityResponse(
    DateOnly Date,
    int DurationMinutes,
    decimal TotalPrice,
    IReadOnlyList<ProfessionalAvailabilityDto> Professionals);

public sealed class AvailabilityQueryValidator : AbstractValidator<AvailabilityQuery>
{
    public AvailabilityQueryValidator(IOptions<SchedulingOptions> options)
    {
        var max = options.Value.MaxServicesPerAppointment;
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.ServiceIds).NotEmpty().WithMessage("Selecione pelo menos um serviço.")
            .Must(ids => ids.Count <= max).WithMessage($"Selecione no máximo {max} serviços.")
            .Must(ids => ids.All(id => id > 0) && ids.Distinct().Count() == ids.Count)
            .WithMessage("Os serviços informados são inválidos ou repetidos.");
        RuleFor(x => x.ProfessionalId).GreaterThan(0).When(x => x.ProfessionalId.HasValue);
    }
}

public sealed class AvailabilityService(
    IApplicationDbContext db,
    ScheduleReader schedule,
    SalonClock clock,
    IValidator<AvailabilityQuery> validator)
{
    public async Task<AvailabilityResponse> GetAsync(AvailabilityQuery query, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(query, ct);
        var services = await LoadActiveServicesAsync(db, query.ServiceIds, ct);
        var duration = services.Sum(s => s.DurationMinutes);

        var professionals = await schedule.GetEligibleProfessionalsAsync(query.ServiceIds, query.ProfessionalId, ct);
        var working = await schedule.GetWorkingRangesAsync(query.Date, ct);

        var result = new List<ProfessionalAvailabilityDto>();
        if (professionals.Count > 0 && working.Count > 0)
        {
            var busy = await schedule.GetBusyRangesAsync(
                professionals.Select(p => p.Id).ToList(), clock.DayRange(query.Date), ct);
            var window = schedule.PublicWindow();

            foreach (var professional in professionals)
            {
                var slots = AvailabilityCalculator.GetAvailableSlots(
                    working, busy[professional.Id], TimeSpan.FromMinutes(duration), schedule.SlotStep,
                    window.EarliestStart, window.LatestStart);
                result.Add(new ProfessionalAvailabilityDto(professional.Id, professional.Name,
                    slots.Select(s => new SlotDto(clock.ToLocal(s.Start), clock.ToLocal(s.End))).ToList()));
            }
        }

        return new AvailabilityResponse(query.Date, duration, services.Sum(s => s.Price), result);
    }

    /// <summary>Loads the services by id; every id must exist and be active (prices/durations never come from the client).</summary>
    public static async Task<List<Service>> LoadActiveServicesAsync(
        IApplicationDbContext db, IReadOnlyCollection<int> serviceIds, CancellationToken ct)
    {
        var services = await db.Services.Where(s => serviceIds.Contains(s.Id) && s.IsActive).ToListAsync(ct);
        if (services.Count != serviceIds.Distinct().Count())
            throw Validation.Error("ServiceIds", "Um ou mais serviços não existem ou não estão disponíveis.");
        return services;
    }
}
