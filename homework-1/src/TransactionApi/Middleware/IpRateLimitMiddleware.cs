using System.Collections.Concurrent;

namespace TransactionApi.Middleware;

public sealed class IpRateLimitTracker
{
    private readonly ConcurrentDictionary<string, object> _locks = new();
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _requestsByIp = new();

    /// <returns> True if the caller should reject the request with 429. </returns>
    public bool ShouldReject(string clientIp, int maxRequests, TimeSpan window)
    {
        var gate = _locks.GetOrAdd(clientIp, _ => new object());
        lock (gate)
            return ShouldRejectLocked(clientIp, maxRequests, window);
    }

    private bool ShouldRejectLocked(string ip, int maxRequests, TimeSpan window)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now - window;
        var list = _requestsByIp.GetOrAdd(ip, _ => new List<DateTimeOffset>(capacity: maxRequests + 1));

        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] < cutoff)
                list.RemoveAt(i);
        }

        if (list.Count >= maxRequests)
            return true;

        list.Add(now);
        return false;
    }
}

/// <summary>
/// Allows up to <see cref="IpRateLimitMiddleware.MaxRequests"/> requests per sliding <see cref="IpRateLimitMiddleware.Window"/>
/// per client IP address. Responds with 429 Too Many Requests when exceeded.
/// </summary>
public sealed class IpRateLimitMiddleware
{
    public const int MaxRequests = 100;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;
    private readonly IpRateLimitTracker _tracker;

    public IpRateLimitMiddleware(RequestDelegate next, IpRateLimitTracker tracker, IHostEnvironment environment)
    {
        _next = next;
        _tracker = tracker;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_environment.IsEnvironment("Testing"))
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (_tracker.ShouldReject(ip, MaxRequests, Window))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { error = "Too many requests. Try again later." });
            return;
        }

        await _next(context);
    }
}
