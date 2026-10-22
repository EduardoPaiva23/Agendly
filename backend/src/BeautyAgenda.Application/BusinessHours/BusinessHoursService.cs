using System.Globalization;
using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BeautyAgenda.Application.BusinessHours;

/// <summary>Times are "HH:mm" in salon local time.</summary>
public sealed record BusinessHourDto(DayOfWeek DayOfWeek, string StartTime, string EndTime);

public sealed record UpdateBusinessHoursRequest(IReadOnlyList<BusinessHourDto> Items);

public sealed class UpdateBusinessHoursRequestValidator : AbstractValidator<UpdateBusinessHoursRequest>
{
    public UpdateBusinessHoursRequestValidator()
    {
        RuleFor(x => x.Items).NotNull().Must(i => i.Count <= 28).WithMessage("At most 28 intervals per week.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.DayOfWeek).IsInEnum();
            item.RuleFor(i => i.StartTime).Must(t => BusinessHoursService.TryParse(t, out _))
                .WithMessage("StartTime must be HH:mm.");
            item.RuleFor(i => i.EndTime).Must(t => BusinessHoursService.TryParse(t, out _))
                .WithMessage("EndTime must be HH:mm.");
            item.RuleFor(i => i.EndTime)
                .Must((i, end) => !BusinessHoursService.TryParse(i.StartTime, out var s)
                                  || !BusinessHoursService.TryParse(end, out var e)
                                  || s < e)
                .WithMessage("StartTime must be before EndTime.");
        });

        RuleFor(x => x.Items)
            .Must(NotOverlapWithinDay)
            .WithMessage("Intervals on the same day must not overlap.")
            .When(x => x.Items is not null && x.Items.All(IsWellFormed));
    }

    private static bool IsWellFormed(BusinessHourDto i) =>
        BusinessHoursService.TryParse(i.StartTime, out var s) && BusinessHoursService.TryParse(i.EndTime, out var e) && s < e;

    private static bool NotOverlapWithinDay(IReadOnlyList<BusinessHourDto> items) =>
        items.GroupBy(i => i.DayOfWeek).All(day =>
        {
            var ordered = day
                .Select(i => (Start: TimeOnly.ParseExact(i.StartTime, "HH:mm"), End: TimeOnly.ParseExact(i.EndTime, "HH:mm")))
                .OrderBy(i => i.Start).ToList();
            return ordered.Zip(ordered.Skip(1)).All(pair => pair.First.End <= pair.Second.Start);
        });
}

public sealed class BusinessHoursService(
    IApplicationDbContext db,
    IValidator<UpdateBusinessHoursRequest> validator,
    AuditWriter audit)
{
    public static bool TryParse(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public async Task<IReadOnlyList<BusinessHourDto>> GetAsync(CancellationToken ct)
    {
        var hours = await db.BusinessHours.AsNoTracking().ToListAsync(ct);
        return hours.OrderBy(h => h.DayOfWeek).ThenBy(h => h.StartTime).Select(ToDto).ToList();
    }

    /// <summary>Replaces the whole weekly grid. Existing appointments are not changed.</summary>
    public async Task<IReadOnlyList<BusinessHourDto>> ReplaceAsync(UpdateBusinessHoursRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        await db.ExecuteInTransactionAsync(async token =>
        {
            db.BusinessHours.RemoveRange(await db.BusinessHours.ToListAsync(token));
            db.BusinessHours.AddRange(request.Items.Select(i => new BusinessHour
            {
                DayOfWeek = i.DayOfWeek,
                StartTime = TimeOnly.ParseExact(i.StartTime, "HH:mm"),
                EndTime = TimeOnly.ParseExact(i.EndTime, "HH:mm"),
            }));
            audit.Add(nameof(BusinessHour), "week", "Replaced", request.Items);
            return await db.SaveChangesAsync(token);
        }, ct);

        return await GetAsync(ct);
    }

    private static BusinessHourDto ToDto(BusinessHour h) =>
        new(h.DayOfWeek, h.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture), h.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture));
}
