using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClaimDesk.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef</c> so migrations are always generated for SQL Server.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ClaimDeskDbContext>
{
    public ClaimDeskDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ClaimDeskDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=ClaimDesk;Integrated Security=false;TrustServerCertificate=True")
            .Options;
        return new ClaimDeskDbContext(options);
    }
}
