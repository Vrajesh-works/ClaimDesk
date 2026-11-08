using System.Net;
using System.Net.Http.Json;
using ClaimDesk.Application.Claims;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Infrastructure.Seeding;
using ClaimDesk.IntegrationTests.Infrastructure;
using Testcontainers.MsSql;

namespace ClaimDesk.IntegrationTests;

/// <summary>Skips unless Docker is available (always available in CI).</summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        var socket = File.Exists("/var/run/docker.sock") || Environment.GetEnvironmentVariable("DOCKER_HOST") is not null;
        if (!socket || Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS") == "1")
        {
            Skip = "Docker is not available; SQL Server tests run in CI.";
        }
    }
}

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

internal sealed class SqlServerApiFactory(string connectionString) : ClaimDeskApiFactory(connectionString);

/// <summary>Runs migrations and the lifecycle against a real SQL Server 2022 container.</summary>
public class SqlServerTests(SqlServerFixture sql) : IClassFixture<SqlServerFixture>
{
    [DockerFact]
    public async Task Migrations_apply_and_lifecycle_works_on_sql_server()
    {
        await using var factory = new SqlServerApiFactory(sql.ConnectionString.Replace("Database=master", "Database=ClaimDeskTests", StringComparison.Ordinal));
        using var claimant = factory.ClientFor(DemoUsers.Claimant);
        using var adjuster = factory.ClientFor(DemoUsers.Adjuster);
        using var admin = factory.ClientFor(DemoUsers.Admin);

        using var ready = await claimant.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        var filed = await (await claimant.PostAsJsonAsync("/api/claims", TestClaims.NewRequest(), TestClaims.Json)).ReadAsync<ClaimDetailDto>();
        using (var upload = await claimant.PostAsync($"/api/claims/{filed.Id}/evidence", TestClaims.FileForm(TestClaims.PdfBytes, "estimate.pdf")))
        {
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        }

        foreach (var (client, action) in new[] { (adjuster, "start-review"), (adjuster, "approve"), (admin, "mark-paid") })
        {
            using var response = await client.PostAsJsonAsync($"/api/claims/{filed.Id}/{action}", new { });
            Assert.True(response.IsSuccessStatusCode, $"{action}: {await response.Content.ReadAsStringAsync()}");
        }

        var queue = await (await adjuster.GetAsync("/api/queue")).ReadAsync<List<ClaimSummaryDto>>();
        Assert.DoesNotContain(queue, c => c.Id == filed.Id);

        var history = await (await admin.GetAsync($"/api/claims/{filed.Id}/history")).ReadAsync<List<ClaimEventDto>>();
        Assert.Equal(ClaimStatus.Paid, history[^1].ToStatus);
        Assert.Equal(6, history.Count);
    }
}
