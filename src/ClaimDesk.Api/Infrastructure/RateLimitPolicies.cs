using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ClaimDesk.Api.Infrastructure;

public static class RateLimitPolicies
{
    public const string Login = "login";

    /// <summary>Fixed-window limit on the login endpoint per client IP to slow password guessing.</summary>
    public static void Configure(RateLimiterOptions options, IConfiguration configuration)
    {
        var permits = configuration.GetValue("RateLimiting:LoginPermitsPerMinute", 10);
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(Login, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    }
}
