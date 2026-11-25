using System.Threading.RateLimiting;
using Vairas.LearningTracking.Middleware;

namespace Vairas.LearningTracking.Configuration;

public static class ConfigureRateLimiting
{
    public static IServiceCollection AddRequestRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                http.Request.Path.StartsWithSegments("/health")
                    ? RateLimitPartition.GetNoLimiter("health")
                    : RateLimitPartition.GetFixedWindowLimiter(
                        http.User.FindFirst("sub")?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        _ => new() { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.WriteProblemAsync(429, "RATE_LIMIT_EXCEEDED", "Request limit exceeded.", ct);
            };
        });
        return services;
    }
}
