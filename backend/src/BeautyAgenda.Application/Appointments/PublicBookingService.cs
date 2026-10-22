using BeautyAgenda.Application.Abstractions;
using BeautyAgenda.Application.Availability;
using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Scheduling;
using BeautyAgenda.Domain.Entities;
using BeautyAgenda.Domain.Scheduling;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeautyAgenda.Application.Appointments;

public sealed record CreatePublicAppointmentRequest(
    int ProfessionalId,
    IReadOnlyList<int> ServiceIds,
    DateTimeOffset StartAt,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail);

public sealed record PublicCancelRequest(string? Reason);

public sealed class CreatePublicAppointmentRequestValidator : AbstractValidator<CreatePublicAppointmentRequest>
{
    public CreatePublicAppointmentRequestValidator(IOptions<SchedulingOptions> options)
    {
        var max = options.Value.MaxServicesPerAppointment;
        RuleFor(x => x.ProfessionalId).GreaterThan(0);
        RuleFor(x => x.ServiceIds).NotEmpty().WithMessage("Select at least one service.")
            .Must(ids => ids.Count <= max).WithMessage($"Select at most {max} services.")
            .Must(ids => ids.All(id => id > 0) && ids.Distinct().Count() == ids.Count)
            .WithMessage("Service ids must be positive and distinct.");
        RuleFor(x => x.StartAt).NotEmpty();
        RuleFor(x => x.CustomerName).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.CustomerPhone).Must(PhoneNumber.IsValid)
            .WithMessage($"Phone must have between {PhoneNumber.MinDigits} and {PhoneNumber.MaxDigits} digits (with area code).");
        RuleFor(x => x.CustomerEmail).EmailAddress().MaximumLength(254)
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail));
    }
}

public sealed class PublicCancelRequestValidator : AbstractValidator<PublicCancelRequest>
{
    public PublicCancelRequestValidator() => RuleFor(x => x.Reason).MaximumLength(300);
}

/// <summary>Customer self-service: book, view and cancel without an account.</summary>
public sealed class PublicBookingService(
    IApplicationDbContext db,
    ScheduleReader schedule,
    SalonClock clock,
    IOptions<SchedulingOptions> options,
    IValidator<CreatePublicAppointmentRequest> validator,
    IValidator<PublicCancelRequest> cancelValidator)
{
    private readonly SchedulingOptions _options = options.Value;

    public async Task<PublicAppointmentDto> CreateAsync(CreatePublicAppointmentRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var services = await AvailabilityService.LoadActiveServicesAsync(db, request.ServiceIds, ct);
        var professional = (await schedule.GetEligibleProfessionalsAsync(request.ServiceIds, request.ProfessionalId, ct))
            .SingleOrDefault()
            ?? throw Validation.Error(nameof(request.ProfessionalId),
                "The selected professional is not available for the selected services.");

        var startUtc = request.StartAt.UtcDateTime;
        var localDate = clock.LocalDateOf(startUtc);
        var phone = PhoneNumber.Normalize(request.CustomerPhone);

        var appointment = await db.ExecuteInTransactionAsync(async token =>
        {
            // Serializes concurrent bookings for this professional: the check below and the insert are atomic.
            await db.LockProfessionalSchedulesAsync([professional.Id], token);

            var working = await schedule.GetWorkingRangesAsync(localDate, token);
            var busy = await schedule.GetBusyRangesAsync([professional.Id], clock.DayRange(localDate), token);
            var window = schedule.PublicWindow();
            var slots = AvailabilityCalculator.GetAvailableSlots(
                working, busy[professional.Id], TimeSpan.FromMinutes(services.Sum(s => s.DurationMinutes)),
                schedule.SlotStep, window.EarliestStart, window.LatestStart);

            if (!slots.Any(s => s.Start == startUtc))
                throw new ConflictException("The selected time is no longer available. Please choose another time.");

            // Returning customers are recognized by phone; their stored data is not overwritten from a public form.
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, token)
                           ?? db.Customers.Add(new Customer
                           {
                               Name = request.CustomerName.Trim(),
                               Phone = phone,
                               Email = string.IsNullOrWhiteSpace(request.CustomerEmail)
                                   ? null
                                   : request.CustomerEmail.Trim().ToLowerInvariant(),
                               CreatedAt = clock.UtcNow,
                           }).Entity;

            var created = Appointment.Create(customer, professional.Id, startUtc, services, PublicReference.New(), clock.UtcNow);
            db.Appointments.Add(created);
            await db.SaveChangesAsync(token);
            return created;
        }, ct);

        return await GetAsync(appointment.PublicReference, ct);
    }

    public async Task<PublicAppointmentDto> GetAsync(string reference, CancellationToken ct)
    {
        var appointment = await FindAsync(reference, tracking: false, ct);
        return ToPublicDto(appointment);
    }

    public async Task<PublicAppointmentDto> CancelAsync(string reference, PublicCancelRequest request, CancellationToken ct)
    {
        await cancelValidator.ValidateAndThrowAsync(request, ct);
        var appointment = await FindAsync(reference, tracking: true, ct);

        if (!appointment.IsActive)
            throw new ConflictException($"This appointment is already {appointment.Status}.");
        if (!CanCancelOnline(appointment))
            throw new ConflictException(
                $"Online cancellation is only possible up to {_options.PublicCancellationMinNoticeHours} hour(s) before the start. Please contact the salon.");

        appointment.Cancel(clock.UtcNow, string.IsNullOrWhiteSpace(request.Reason) ? "Cancelled by customer" : request.Reason);
        await db.SaveChangesAsync(ct);
        return ToPublicDto(appointment);
    }

    private async Task<Appointment> FindAsync(string reference, bool tracking, CancellationToken ct)
    {
        // Malformed references are answered exactly like unknown ones: no hint for enumeration.
        if (!PublicReference.IsWellFormed(reference))
            throw new NotFoundException("Appointment", "reference");

        var query = db.Appointments
            .Include(a => a.Professional)
            .Include(a => a.Services).ThenInclude(s => s.Service)
            .AsQueryable();
        if (!tracking) query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(a => a.PublicReference == reference, ct)
               ?? throw new NotFoundException("Appointment", "reference");
    }

    private bool CanCancelOnline(Appointment a) =>
        a.IsActive && a.StartAt - clock.UtcNow >= TimeSpan.FromHours(_options.PublicCancellationMinNoticeHours);

    private PublicAppointmentDto ToPublicDto(Appointment a) => new(
        a.PublicReference,
        a.Status,
        clock.ToLocal(a.StartAt),
        clock.ToLocal(a.EndAt),
        a.Professional.Name,
        AppointmentMapping.MapServices(a),
        a.TotalPrice,
        CanCancelOnline(a));
}
