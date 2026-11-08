using ClaimDesk.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimDesk.Infrastructure.Persistence;

/// <summary>Applies migrations (SQL Server) or creates the schema (SQLite), then seeds demo data if enabled.</summary>
public sealed class DatabaseInitializer(
    ClaimDeskDbContext db,
    DatabaseSeeder seeder,
    IOptions<SeedOptions> seedOptions,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        if (db.Database.IsSqlServer())
        {
            logger.LogInformation("Applying SQL Server migrations");
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            EnsureSqliteDirectory();
            await db.Database.EnsureCreatedAsync(ct);
        }

        if (seedOptions.Value.Enabled)
        {
            await seeder.SeedAsync(ct);
        }
    }

    private void EnsureSqliteDirectory()
    {
        var dataSource = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        if (string.IsNullOrEmpty(dataSource) || dataSource == ":memory:" || dataSource.StartsWith("file:", StringComparison.Ordinal))
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
