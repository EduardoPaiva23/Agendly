using BeautyAgenda.Application.Common;
using BeautyAgenda.Domain.Entities;
using BeautyAgenda.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeautyAgenda.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b) => b.Property(u => u.FullName).HasMaxLength(120);
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.Property(c => c.Name).HasMaxLength(120).IsRequired();
        b.Property(c => c.Phone).HasMaxLength(PhoneNumber.MaxDigits).IsUnicode(false).IsRequired();
        b.Property(c => c.Email).HasMaxLength(254);
        b.HasIndex(c => c.Phone).IsUnique();
        b.HasIndex(c => c.Name);
    }
}

internal sealed class ProfessionalConfiguration : IEntityTypeConfiguration<Professional>
{
    public void Configure(EntityTypeBuilder<Professional> b)
    {
        b.Property(p => p.Name).HasMaxLength(120).IsRequired();
        b.Property(p => p.Phone).HasMaxLength(PhoneNumber.MaxDigits).IsUnicode(false);
    }
}

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> b)
    {
        b.Property(s => s.Name).HasMaxLength(100).IsRequired();
        b.Property(s => s.Description).HasMaxLength(500);
        b.Property(s => s.Price).HasPrecision(10, 2);
        b.HasIndex(s => s.Name).IsUnique();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Services_Duration", "[DurationMinutes] > 0");
            t.HasCheckConstraint("CK_Services_Price", "[Price] >= 0");
        });
    }
}

internal sealed class ProfessionalServiceConfiguration : IEntityTypeConfiguration<ProfessionalService>
{
    public void Configure(EntityTypeBuilder<ProfessionalService> b)
    {
        b.HasKey(ps => new { ps.ProfessionalId, ps.ServiceId });
        b.HasOne(ps => ps.Professional).WithMany(p => p.Services).HasForeignKey(ps => ps.ProfessionalId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(ps => ps.Service).WithMany(s => s.Professionals).HasForeignKey(ps => ps.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(ps => ps.ServiceId);
    }
}

internal sealed class BusinessHourConfiguration : IEntityTypeConfiguration<BusinessHour>
{
    public void Configure(EntityTypeBuilder<BusinessHour> b)
    {
        b.HasIndex(h => new { h.DayOfWeek, h.StartTime }).IsUnique();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_BusinessHours_Range", "[StartTime] < [EndTime]");
            t.HasCheckConstraint("CK_BusinessHours_Day", "[DayOfWeek] BETWEEN 0 AND 6");
        });
    }
}

internal sealed class ProfessionalTimeOffConfiguration : IEntityTypeConfiguration<ProfessionalTimeOff>
{
    public void Configure(EntityTypeBuilder<ProfessionalTimeOff> b)
    {
        b.Property(t => t.Reason).HasMaxLength(200);
        b.HasOne(t => t.Professional).WithMany(p => p.TimeOffs).HasForeignKey(t => t.ProfessionalId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(t => new { t.ProfessionalId, t.StartAt, t.EndAt });
        b.ToTable(t => t.HasCheckConstraint("CK_ProfessionalTimeOffs_Range", "[StartAt] < [EndAt]"));
    }
}

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.Property(a => a.PublicReference).HasMaxLength(PublicReference.Length).IsUnicode(false).IsFixedLength().IsRequired();
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.CancellationReason).HasMaxLength(300);
        b.Property(a => a.RowVersion).IsRowVersion();

        b.HasOne(a => a.Customer).WithMany(c => c.Appointments).HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.Professional).WithMany(p => p.Appointments).HasForeignKey(a => a.ProfessionalId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(a => a.PublicReference).IsUnique();
        // Overlap checks: WHERE ProfessionalId = @p AND StartAt < @end AND EndAt > @start
        b.HasIndex(a => new { a.ProfessionalId, a.StartAt, a.EndAt });
        b.HasIndex(a => new { a.StartAt, a.Status });
        b.HasIndex(a => a.CustomerId);

        b.Ignore(a => a.Range);
        b.Ignore(a => a.IsActive);
        b.Ignore(a => a.TotalDurationMinutes);
        b.Ignore(a => a.TotalPrice);

        b.ToTable(t => t.HasCheckConstraint("CK_Appointments_Range", "[StartAt] < [EndAt]"));
    }
}

internal sealed class AppointmentServiceConfiguration : IEntityTypeConfiguration<AppointmentService>
{
    public void Configure(EntityTypeBuilder<AppointmentService> b)
    {
        b.Property(s => s.PriceAtBooking).HasPrecision(10, 2);
        b.HasOne(s => s.Appointment).WithMany(a => a.Services).HasForeignKey(s => s.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
        // Services with booking history cannot be deleted (they are deactivated instead).
        b.HasOne(s => s.Service).WithMany().HasForeignKey(s => s.ServiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(s => s.AppointmentId);
        b.ToTable(t => t.HasCheckConstraint("CK_AppointmentServices_Duration", "[DurationMinutes] > 0"));
    }
}

internal sealed class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> b)
    {
        b.Property(l => l.UserId).HasMaxLength(450).IsRequired();
        b.Property(l => l.EntityName).HasMaxLength(100).IsRequired();
        b.Property(l => l.EntityId).HasMaxLength(64).IsRequired();
        b.Property(l => l.Action).HasMaxLength(50).IsRequired();
        b.Property(l => l.Details).HasMaxLength(4000);
        b.HasIndex(l => new { l.EntityName, l.EntityId });
        b.HasIndex(l => l.CreatedAt);
    }
}
