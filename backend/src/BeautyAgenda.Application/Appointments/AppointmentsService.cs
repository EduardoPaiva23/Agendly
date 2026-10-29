using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Scheduling;
using BeautyAgenda.Domain.Entities;
using BeautyAgenda.Domain.Scheduling;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BeautyAgenda.Application.Appointments;

public sealed record AppointmentListQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? ProfessionalId,
    int? CustomerId,
    AppointmentStatus? Status,
    int? Page,
    int? PageSize);

public sealed record ChangeStatusRequest(AppointmentStatus Status, string? Reason);

public sealed record RescheduleRequest(DateTimeOffset StartAt, int? ProfessionalId);

public sealed record CancelAppointmentRequest(string? Reason);

public sealed class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
{
    public ChangeStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

public sealed class RescheduleRequestValidator : AbstractValidator<RescheduleRequest>
{
    public RescheduleRequestValidator()
    {
        RuleFor(x => x.StartAt).NotEmpty();
        RuleFor(x => x.ProfessionalId).GreaterThan(0).When(x => x.ProfessionalId.HasValue);
    }
}

public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator() => RuleFor(x => x.Reason).MaximumLength(300);
}

/// <summary>Staff operations over the appointment lifecycle.</summary>
public sealed class AppointmentsService(
    IApplicationDbContext db,
    ScheduleReader schedule,
    SalonClock clock,
    AuditWriter audit,
    IValidator<ChangeStatusRequest> statusValidator,
    IValidator<RescheduleRequest> rescheduleValidator,
    IValidator<CancelAppointmentRequest> cancelValidator)
{
    public async Task<PagedResult<AppointmentDto>> ListAsync(AppointmentListQuery query, CancellationToken ct)
    {
        var (page, size) = Paging.Normalize(query.Page, query.PageSize);
        var q = WithDetails(db.Appointments.AsNoTracking());

        if (query.From is { } from) { var f = from.UtcDateTime; q = q.Where(a => a.EndAt > f); }
        if (query.To is { } to) { var t = to.UtcDateTime; q = q.Where(a => a.StartAt < t); }
        if (query.ProfessionalId is { } pid) q = q.Where(a => a.ProfessionalId == pid);
        if (query.CustomerId is { } cid) q = q.Where(a => a.CustomerId == cid);
        if (query.Status is { } status) q = q.Where(a => a.Status == status);

        var (items, total) = await q.OrderBy(a => a.StartAt).ThenBy(a => a.Id).PageAsync(page, size, ct);
        return new PagedResult<AppointmentDto>(items.Select(a => a.ToDto(clock)).ToList(), page, size, total);
    }

    public async Task<AppointmentDto> GetAsync(int id, CancellationToken ct) =>
        (await LoadAsync(id, tracking: false, ct)).ToDto(clock);

    public async Task<AppointmentDto> ChangeStatusAsync(int id, ChangeStatusRequest request, CancellationToken ct)
    {
        await statusValidator.ValidateAndThrowAsync(request, ct);
        var appointment = await LoadAsync(id, tracking: true, ct);
        var previous = appointment.Status;

        appointment.ChangeStatus(request.Status, clock.UtcNow, request.Reason);
        audit.Add(nameof(Appointment), id, "StatusChanged", new { from = previous, to = request.Status, request.Reason });
        await db.SaveChangesAsync(ct);
        return appointment.ToDto(clock);
    }

    public async Task<AppointmentDto> CancelAsync(int id, CancelAppointmentRequest request, CancellationToken ct)
    {
        await cancelValidator.ValidateAndThrowAsync(request, ct);
        var appointment = await LoadAsync(id, tracking: true, ct);
        var previous = appointment.Status;

        appointment.Cancel(clock.UtcNow, request.Reason);
        audit.Add(nameof(Appointment), id, "Cancelled", new { from = previous, request.Reason });
        await db.SaveChangesAsync(ct);
        return appointment.ToDto(clock);
    }

    /// <summary>
    /// Moves an active appointment, optionally to another professional who performs all its services.
    /// Contracted prices are kept. Availability is re-checked under the schedule lock; on any failure nothing changes.
    /// </summary>
    public async Task<AppointmentDto> RescheduleAsync(int id, RescheduleRequest request, CancellationToken ct)
    {
        await rescheduleValidator.ValidateAndThrowAsync(request, ct);
        var startUtc = request.StartAt.UtcDateTime;

        var appointment = await db.ExecuteInTransactionAsync(async token =>
        {
            var current = await LoadAsync(id, tracking: true, token);
            if (!current.IsActive)
                throw new ConflictException($"Um agendamento {AppointmentStatusText.Of(current.Status)} não pode ser reagendado.");

            var targetProfessionalId = request.ProfessionalId ?? current.ProfessionalId;
            var serviceIds = current.Services.Select(s => s.ServiceId).Distinct().ToList();

            if (targetProfessionalId != current.ProfessionalId)
            {
                var eligible = await db.Professionals.AnyAsync(p => p.Id == targetProfessionalId && p.IsActive
                    && p.Services.Count(s => serviceIds.Contains(s.ServiceId)) == serviceIds.Count, token);
                if (!eligible)
                    throw Validation.Error(nameof(request.ProfessionalId),
                        "O profissional escolhido está inativo ou não realiza todos os serviços deste agendamento.");
            }

            await db.LockProfessionalSchedulesAsync([current.ProfessionalId, targetProfessionalId], token);

            var candidate = new TimeRange(startUtc, startUtc.AddMinutes(current.TotalDurationMinutes));
            var localDate = clock.LocalDateOf(startUtc);
            var working = await schedule.GetWorkingRangesAsync(localDate, token);
            var busy = await schedule.GetBusyRangesAsync([targetProfessionalId], clock.DayRange(localDate), token,
                ignoreAppointmentId: current.Id);
            var window = schedule.StaffWindow();

            if (!AvailabilityCalculator.IsAvailable(candidate, working, busy[targetProfessionalId],
                    window.EarliestStart, window.LatestStart))
                throw new ConflictException(
                    "O novo horário não está disponível (fora do expediente, no passado ou sobrepondo outro agendamento ou bloqueio).");

            var before = new { current.ProfessionalId, current.StartAt, current.EndAt };
            current.Reschedule(targetProfessionalId, startUtc, clock.UtcNow);
            audit.Add(nameof(Appointment), id, "Rescheduled",
                new { before, after = new { current.ProfessionalId, current.StartAt, current.EndAt } });
            await db.SaveChangesAsync(token);
            return current;
        }, ct);

        return await GetAsync(appointment.Id, ct);
    }

    private static IQueryable<Appointment> WithDetails(IQueryable<Appointment> q) =>
        q.Include(a => a.Customer)
            .Include(a => a.Professional)
            .Include(a => a.Services).ThenInclude(s => s.Service);

    private async Task<Appointment> LoadAsync(int id, bool tracking, CancellationToken ct)
    {
        var q = WithDetails(db.Appointments);
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Appointment", id);
    }
}
