using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Data;

/// <summary>
/// Lets `dotnet ef` create the context without booting the whole app (which needs JWT secrets).
/// The connection string comes from the same places as the app: appsettings, user-secrets, then the
/// ConnectionStrings__DefaultConnection environment variable. Nothing is hard-coded.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    // Used only by `migrations add`, which never connects. `database update` needs the real one configured.
    private const string PlaceholderConnection =
        "Server=localhost,1433;Database=ECommerce;User Id=sa;Password=not-configured;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
            connection = PlaceholderConnection;

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
        return new AppDbContext(options);
    }
}
