using BeautyAgenda.Application.Common;
using BeautyAgenda.Application.Scheduling;
using BeautyAgenda.Domain.Entities;
using BeautyAgenda.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BeautyAgenda.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string Section = "Database";
    public bool ApplyMigrationsOnStartup { get; set; }
}

public sealed class SeedOptions
{
    public const string Section = "Seed";
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string AdminName { get; set; } = "Administrador";
    public string? StaffEmail { get; set; }
    public string? StaffPassword { get; set; }
    public string StaffName { get; set; } = "Recepção";
    /// <summary>Inserts sample services, professionals, hours and appointments when the catalog is empty.</summary>
    public bool DemoData { get; set; }
}

public sealed class DatabaseInitializer(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles,
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<SeedOptions> seedOptions,
    SalonClock clock,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (databaseOptions.Value.ApplyMigrationsOnStartup)
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(ct);
        }

        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        var seed = seedOptions.Value;
        await EnsureUserAsync(seed.AdminEmail, seed.AdminPassword, seed.AdminName, Roles.Admin);
        await EnsureUserAsync(seed.StaffEmail, seed.StaffPassword, seed.StaffName, Roles.Staff);

        if (seed.DemoData && !await db.Services.AnyAsync(ct))
            await SeedDemoDataAsync(ct);
    }

    private async Task EnsureUserAsync(string? email, string? password, string name, string role)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (await users.FindByEmailAsync(email) is not null) return;

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = name };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            // Errors describe password rules, never the password itself.
            throw new InvalidOperationException(
                $"Could not create seed user ({role}): {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
        await users.AddToRoleAsync(user, role);
        logger.LogInformation("Seed user created with role {Role}", role);
    }

    private async Task SeedDemoDataAsync(CancellationToken ct)
    {
        logger.LogInformation("Seeding demo data");

        var services = new[]
        {
            new Service { Name = "Corte feminino", Description = "Lavagem, corte e finalização.", DurationMinutes = 60, Price = 90m },
            new Service { Name = "Corte masculino", Description = "Corte na tesoura ou máquina.", DurationMinutes = 30, Price = 45m },
            new Service { Name = "Escova", Description = "Escova modelada.", DurationMinutes = 45, Price = 60m },
            new Service { Name = "Coloração", Description = "Coloração completa com produtos profissionais.", DurationMinutes = 120, Price = 220m },
            new Service { Name = "Manicure", Description = "Cutilagem e esmaltação.", DurationMinutes = 45, Price = 40m },
            new Service { Name = "Pedicure", Description = "Cutilagem, esfoliação e esmaltação.", DurationMinutes = 60, Price = 50m },
            new Service { Name = "Design de sobrancelha", Description = "Modelagem com pinça e linha.", DurationMinutes = 30, Price = 35m },
        };
        db.Services.AddRange(services);

        Professional Pro(string name, params Service[] offered) => new()
        {
            Name = name,
            Services = offered.Select(s => new ProfessionalService { Service = s }).ToList(),
        };

        var ana = Pro("Ana Souza", services[0], services[1], services[2], services[3]);
        var bruno = Pro("Bruno Lima", services[1], services[0]);
        var carla = Pro("Carla Mendes", services[4], services[5], services[6]);
        db.Professionals.AddRange(ana, bruno, carla);

        var weekdays = new[] { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        foreach (var day in weekdays)
        {
            db.BusinessHours.Add(new BusinessHour { DayOfWeek = day, StartTime = new(9, 0), EndTime = new(12, 0) });
            db.BusinessHours.Add(new BusinessHour { DayOfWeek = day, StartTime = new(13, 0), EndTime = new(19, 0) });
        }
        db.BusinessHours.Add(new BusinessHour { DayOfWeek = DayOfWeek.Saturday, StartTime = new(8, 0), EndTime = new(14, 0) });

        var customers = new[]
        {
            new Customer { Name = "Juliana Alves", Phone = "11987654321", Email = "juliana@example.com", CreatedAt = clock.UtcNow },
            new Customer { Name = "Marcos Pereira", Phone = "11912345678", CreatedAt = clock.UtcNow },
            new Customer { Name = "Fernanda Costa", Phone = "21998887766", Email = "fernanda@example.com", CreatedAt = clock.UtcNow },
        };
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync(ct);

        // A few upcoming appointments on the next working days so the calendar is not empty.
        var date = clock.Today.AddDays(1);
        var created = 0;
        while (created < 3)
        {
            if (weekdays.Contains(date.DayOfWeek))
            {
                db.Appointments.Add(Appointment.Create(customers[created], ana.Id, clock.ToUtc(date, new TimeOnly(10, 0)),
                    [services[0]], PublicReference.New(), clock.UtcNow));
                db.Appointments.Add(Appointment.Create(customers[(created + 1) % 3], carla.Id, clock.ToUtc(date, new TimeOnly(14, 0)),
                    [services[4], services[6]], PublicReference.New(), clock.UtcNow));
                created++;
            }
            date = date.AddDays(1);
        }
        await db.SaveChangesAsync(ct);
    }
}
