using System.Text.Json;
using API.Tickets;
using Application;
using Application.Tickets;
using Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration["TelegramBot:Token"]);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
    await repository.Initialize();
}

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/health", () => Results.Ok(new HealthCheckResponse("ok", "CustomerSupportSystem")))
    .WithName("HealthCheck");
app.MapTicketEndpoints();

app.Run();

public partial class Program;

internal sealed record HealthCheckResponse(string Status, string Service);
