using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TransactionApi.Middleware;
using Xunit;

namespace TransactionApi.Tests.Middleware;

public class IpRateLimitMiddlewareTests
{
    private sealed class FixedHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "TransactionApi.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static DefaultHttpContext CreateContext(IPAddress? remoteIp)
    {
        var ctx = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() }
        };
        ctx.Connection.RemoteIpAddress = remoteIp;
        return ctx;
    }

    [Fact]
    public async Task Middleware_TestingEnvironment_DoesNotEnforceLimits()
    {
        var tracker = new IpRateLimitTracker();
        var env = new FixedHostEnvironment { EnvironmentName = "Testing" };
        var nextCalls = 0;
        RequestDelegate next = _ =>
        {
            Interlocked.Increment(ref nextCalls);
            return Task.CompletedTask;
        };
        var middleware = new IpRateLimitMiddleware(next, tracker, env);
        var ip = IPAddress.Parse("10.1.2.8");

        for (var i = 0; i < 105; i++)
            await middleware.InvokeAsync(CreateContext(ip));

        Assert.Equal(105, nextCalls);
    }

    [Fact]
    public async Task Middleware_NonTesting_AllowsExactly100Then429()
    {
        var tracker = new IpRateLimitTracker();
        var env = new FixedHostEnvironment { EnvironmentName = Environments.Development };
        var nextCalls = 0;
        RequestDelegate next = _ =>
        {
            Interlocked.Increment(ref nextCalls);
            return Task.CompletedTask;
        };
        var middleware = new IpRateLimitMiddleware(next, tracker, env);
        var ip = IPAddress.Parse("172.21.49.99");

        for (var i = 0; i < 100; i++)
        {
            var ctx = CreateContext(ip);
            await middleware.InvokeAsync(ctx);
            Assert.NotEqual(StatusCodes.Status429TooManyRequests, ctx.Response.StatusCode);
        }

        Assert.Equal(100, Volatile.Read(ref nextCalls));

        var blocked = CreateContext(ip);
        await middleware.InvokeAsync(blocked);
        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.Equal(100, Volatile.Read(ref nextCalls));

        blocked.Response.Body.Seek(0, SeekOrigin.Begin);
        using var sr = new StreamReader(blocked.Response.Body);
        var body = await sr.ReadToEndAsync();
        Assert.Contains("Too many requests", body, StringComparison.OrdinalIgnoreCase);
    }
}
