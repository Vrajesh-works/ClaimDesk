using ClaimDesk.Application.Abstractions;
using ClaimDesk.Application.Claims;
using ClaimDesk.Application.Risk;
using ClaimDesk.Application.Sla;
using ClaimDesk.Domain.Users;
using ClaimDesk.Infrastructure.Identity;
using ClaimDesk.Infrastructure.Notifications;
using ClaimDesk.Infrastructure.Persistence;
using ClaimDesk.Infrastructure.Seeding;
using ClaimDesk.Infrastructure.Sla;
using ClaimDesk.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ClaimDesk.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "ClaimDesk";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing.");

        services.AddDbContext<ClaimDeskDbContext>(options =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5));
            }
        });
        services.AddScoped<IClaimDeskDbContext>(sp => sp.GetRequiredService<ClaimDeskDbContext>());

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IRiskScorer, RiskScorer>();
        services.AddScoped<ClaimService>();
        services.AddScoped<SlaScanner>();
        services.AddScoped<UserAuthenticator>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<DatabaseInitializer>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<INotificationSink, LoggingNotificationSink>();

        services.AddOptions<SlaOptions>().Bind(configuration.GetSection(SlaOptions.Section));
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.Section));
        services.AddOptions<EvidenceStorageOptions>()
            .Bind(configuration.GetSection(EvidenceStorageOptions.Section))
            .Validate(o => o.SigningKey.Length >= 32, "EvidenceStorage:SigningKey must be at least 32 characters.")
            .ValidateOnStart();
        services.AddSingleton(sp => new SignedLinkProtector(
            sp.GetRequiredService<IOptions<EvidenceStorageOptions>>().Value.SigningKey,
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IEvidenceStore, LocalDiskEvidenceStore>();

        if (configuration.GetValue("Sla:Enabled", true))
        {
            services.AddHostedService<SlaWorker>();
        }

        return services;
    }
}
