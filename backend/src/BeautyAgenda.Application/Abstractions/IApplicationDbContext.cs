using BeautyAgenda.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BeautyAgenda.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<Professional> Professionals { get; }
    DbSet<Service> Services { get; }
    DbSet<ProfessionalService> ProfessionalServices { get; }
    DbSet<BusinessHour> BusinessHours { get; }
    DbSet<ProfessionalTimeOff> ProfessionalTimeOffs { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<AppointmentService> AppointmentServices { get; }
    DbSet<AdminAuditLog> AdminAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs the operation inside a database transaction; commits on success, rolls back on exception.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    /// <summary>
    /// Takes an exclusive (update) lock on the professionals' rows until the current transaction ends,
    /// serializing every schedule write (booking, rescheduling, time-off) for those professionals.
    /// </summary>
    Task LockProfessionalSchedulesAsync(IEnumerable<int> professionalIds, CancellationToken cancellationToken);
}
