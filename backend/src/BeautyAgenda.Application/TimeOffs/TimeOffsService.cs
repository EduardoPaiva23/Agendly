using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Scheduling;
using BeautyAgenda.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BeautyAgenda.Application.TimeOffs;

public sealed record TimeOffDto(int Id, int ProfessionalId, DateTimeOffset StartAt, DateTimeOffset EndAt, string? Reason);

public sealed record CreateTimeOffRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, string? Reason);

public sealed class CreateTimeOffRequestValidator : AbstractValidator<CreateTimeOffRequest>
{
    public CreateTimeOffRequestValidator()
    {
        RuleFor(x => x.StartAt).NotEmpty();
        RuleFor(x => x.EndAt).NotEmpty().GreaterThan(x => x.StartAt).WithMessage("O fim deve ser depois do início.");
        RuleFor(x => x).Must(x => x.EndAt - x.StartAt <= TimeSpan.FromDays(90))
            .WithName("EndAt").WithMessage("Um bloqueio não pode durar mais de 90 dias.");
        RuleFor(x => x.Reason).MaximumLength(200);
    }
}

public sealed class TimeOffsService(
    IApplicationDbContext db,
    IValidator<CreateTimeOffRequest> validator,
    SalonClock clock,
    AuditWriter audit)
{
    public async Task<IReadOnlyList<TimeOffDto>> ListAsync(int professionalId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        await EnsureProfessionalExistsAsync(professionalId, ct);
        var fromUtc = from?.UtcDateTime ?? clock.UtcNow.AddDays(-1);
        var toUtc = to?.UtcDateTime ?? DateTime.MaxValue;

        var items = await db.ProfessionalTimeOffs.AsNoTracking()
            .Where(t => t.ProfessionalId == professionalId && t.EndAt > fromUtc && t.StartAt < toUtc)
            .OrderBy(t => t.StartAt)
            .Take(500)
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    /// <summary>Rejected with 409 if it overlaps active appointments: staff must reschedule/cancel them first.</summary>
    public async Task<TimeOffDto> CreateAsync(int professionalId, CreateTimeOffRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureProfessionalExistsAsync(professionalId, ct);

        var startUtc = request.StartAt.UtcDateTime;
        var endUtc = request.EndAt.UtcDateTime;

        var timeOff = await db.ExecuteInTransactionAsync(async token =>
        {
            await db.LockProfessionalSchedulesAsync([professionalId], token);

            var conflicts = await db.Appointments
                .CountAsync(a => a.ProfessionalId == professionalId
                                 && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed)
                                 && a.StartAt < endUtc && a.EndAt > startUtc, token);
            if (conflicts > 0)
                throw new ConflictException(
                    $"O período coincide com {conflicts} agendamento(s) ativo(s). Reagende ou cancele antes de bloquear.");

            var entity = new ProfessionalTimeOff
            {
                ProfessionalId = professionalId,
                StartAt = startUtc,
                EndAt = endUtc,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            };
            db.ProfessionalTimeOffs.Add(entity);
            await db.SaveChangesAsync(token);

            audit.Add(nameof(ProfessionalTimeOff), entity.Id, "Created", new { professionalId, startUtc, endUtc });
            await db.SaveChangesAsync(token);
            return entity;
        }, ct);

        return ToDto(timeOff);
    }

    public async Task DeleteAsync(int professionalId, int timeOffId, CancellationToken ct)
    {
        var timeOff = await db.ProfessionalTimeOffs
                          .FirstOrDefaultAsync(t => t.Id == timeOffId && t.ProfessionalId == professionalId, ct)
                      ?? throw new NotFoundException("Time-off", timeOffId);

        db.ProfessionalTimeOffs.Remove(timeOff);
        audit.Add(nameof(ProfessionalTimeOff), timeOffId, "Deleted",
            new { professionalId, timeOff.StartAt, timeOff.EndAt, timeOff.Reason });
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureProfessionalExistsAsync(int professionalId, CancellationToken ct)
    {
        if (!await db.Professionals.AnyAsync(p => p.Id == professionalId, ct))
            throw new NotFoundException("Professional", professionalId);
    }

    private TimeOffDto ToDto(ProfessionalTimeOff t) =>
        new(t.Id, t.ProfessionalId, clock.ToLocal(t.StartAt), clock.ToLocal(t.EndAt), t.Reason);
}
