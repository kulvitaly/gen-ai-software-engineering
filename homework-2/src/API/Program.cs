using Application;
using Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/health", () => Results.Ok(new HealthCheckResponse("ok", "CustomerSupportSystem")))
    .WithName("HealthCheck");

app.Run();

public partial class Program;

internal sealed record HealthCheckResponse(string Status, string Service);
