using System.Net.Http.Headers;
using ClaimDesk.Api.Auth;
using ClaimDesk.Domain.Users;
using ClaimDesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ClaimDesk.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory. Defaults to a private SQLite in-memory database; pass a
/// SQL Server connection string to run the same pipeline against SQL Server.
/// </summary>
public class ClaimDeskApiFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Test-Password-123!";

    private readonly string _provider;
    private readonly string _connectionString;
    private readonly SqliteConnection? _keepAlive;
    private readonly string _evidenceRoot = Path.Combine(Path.GetTempPath(), "claimdesk-tests", Guid.NewGuid().ToString("N"));

    public ClaimDeskApiFactory()
        : this(null)
    {
    }

    protected ClaimDeskApiFactory(string? sqlServerConnectionString)
    {
        if (sqlServerConnectionString is null)
        {
            _provider = "Sqlite";
            _connectionString = $"Data Source=file:claimdesk-{Guid.NewGuid():N}?mode=memory&cache=shared";
            _keepAlive = new SqliteConnection(_connectionString);
            _keepAlive.Open();
        }
        else
        {
            _provider = "SqlServer";
            _connectionString = sqlServerConnectionString;
        }
    }

    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>Extra configuration for a specific test; set before the first request.</summary>
    public Dictionary<string, string> ExtraSettings { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting (not ConfigureAppConfiguration) so values are visible while Program.cs
        // is still building the service collection.
        var settings = new Dictionary<string, string>
        {
            ["Database:Provider"] = _provider,
            ["ConnectionStrings:ClaimDesk"] = _connectionString,
            ["Jwt:SigningKey"] = "integration-test-jwt-signing-key-0123456789abcdef",
            ["EvidenceStorage:SigningKey"] = "integration-test-evidence-link-key-0123456789abcdef",
            ["EvidenceStorage:RootPath"] = _evidenceRoot,
            ["Sla:Enabled"] = "false",
            ["Seed:Enabled"] = "true",
            ["Seed:SampleClaims"] = "0",
            ["Seed:DemoPassword"] = DemoPassword,
            ["RateLimiting:LoginPermitsPerMinute"] = "1000",
        };
        foreach (var (key, value) in settings.Concat(ExtraSettings))
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    public HttpClient ClientFor(User? user)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (user is not null)
        {
            var token = Services.GetRequiredService<TokenIssuer>().Issue(user);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        }

        return client;
    }

    public async Task<T> WithDbAsync<T>(Func<ClaimDeskDbContext, IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaimDeskDbContext>();
        return await action(db, scope.ServiceProvider);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive?.Dispose();
            if (Directory.Exists(_evidenceRoot))
            {
                Directory.Delete(_evidenceRoot, recursive: true);
            }
        }
    }
}
