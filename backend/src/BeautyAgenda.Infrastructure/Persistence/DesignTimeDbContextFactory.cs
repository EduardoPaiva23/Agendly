using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BeautyAgenda.Infrastructure.Persistence;

/// <summary>
/// Used by `dotnet ef` so migrations can be generated without running the API or its secrets.
/// For `database update`, set BEAUTYAGENDA_CONNECTION or pass --connection.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("BEAUTYAGENDA_CONNECTION")
                         ?? "Server=localhost,14333;Database=BeautyAgenda;User Id=sa;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
        return new AppDbContext(options);
    }
}
