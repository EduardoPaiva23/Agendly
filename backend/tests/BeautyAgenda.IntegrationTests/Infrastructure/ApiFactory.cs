using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BeautyAgenda.Application.Auth;
using BeautyAgenda.Application.Scheduling;
using BeautyAgenda.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.MsSql;

namespace BeautyAgenda.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API against a disposable SQL Server container (migrations applied on startup).
/// Scheduling uses a fake clock fixed at Monday 2030-03-04 09:00 (São Paulo), so tests are deterministic.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@test.local";
    public const string StaffEmail = "staff@test.local";
    public const string Password = "Test-Password-123";

    /// <summary>Monday 2030-03-04 09:00 in America/Sao_Paulo (UTC-3).</summary>
    public static readonly DateTimeOffset Now = new(2030, 3, 4, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2030, 3, 4);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public FakeTimeProvider Clock { get; } = new(Now);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connection = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "BeautyAgendaTests" };

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connection.ConnectionString);
        builder.UseSetting("Jwt:Key", "integration-tests-signing-key-0123456789-abcdef");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Seed:DemoData", "false");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", Password);
        builder.UseSetting("Seed:StaffEmail", StaffEmail);
        builder.UseSetting("Seed:StaffPassword", Password);
        builder.UseSetting("RateLimiting:PublicWritePerMinute", "100000");
        builder.UseSetting("RateLimiting:LoginPerMinute", "100000");

        builder.ConfigureServices(services =>
        {
            // Only the salon clock is faked; JWT validation and Identity keep the real clock.
            services.RemoveAll<SalonClock>();
            services.AddSingleton(sp => new SalonClock(Clock, sp.GetRequiredService<IOptions<SalonOptions>>()));
        });
    }

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        var admin = await CreateClientAsAsync(AdminEmail);
        var week = Enum.GetValues<DayOfWeek>()
            .Where(d => d != DayOfWeek.Sunday)
            .Select(d => new { dayOfWeek = d.ToString(), startTime = "09:00", endTime = "18:00" });
        (await admin.PutAsJsonAsync("/api/business-hours", new { items = week }, Json)).EnsureSuccessStatusCode();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }

    public async Task<HttpClient> CreateClientAsAsync(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password), Json);
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
