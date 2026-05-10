using FluentValidation;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using System.Text;
using TransactionApi.Application.Commands.CreateTransaction;
using TransactionApi.Application.Queries.GetAccountBalance;
using TransactionApi.Application.Queries.ExportTransactionsCsv;
using TransactionApi.Application.Queries.GetAccountInterest;
using TransactionApi.Application.Queries.GetAccountSummary;
using TransactionApi.Application.Queries.GetAllTransactions;
using TransactionApi.Application.Queries.GetTransactionById;
using TransactionApi.Domain;
using TransactionApi.Infrastructure;
using TransactionApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddOpenApi();

// Configure Database — shared in-memory SQLite (see Microsoft.Data.Sqlite shared-cache; match ConnectionStrings:DefaultConnection in appsettings.json)
const string sharedInMemorySqlite = "Data Source=TransactionApi;Mode=Memory;Cache=Shared";
var sqliteConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(sqliteConnectionString))
    sqliteConnectionString = sharedInMemorySqlite;

builder.Services.AddSingleton(_ =>
{
    var connection = new SqliteConnection(sqliteConnectionString);
    connection.Open();
    return connection;
});

builder.Services.AddDbContext<TransactionDbContext>(options =>
    options.UseSqlite(sqliteConnectionString));

builder.Services.AddScoped<ITransactionDbContext>(sp => sp.GetRequiredService<TransactionDbContext>());

builder.Services.AddSingleton<IpRateLimitTracker>();

// Configure MediatR
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(CreateTransactionCommand).Assembly);
});

// Configure FluentValidation - manually register validators
builder.Services.AddScoped<IValidator<CreateTransactionCommand>, CreateTransactionCommandValidator>();
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

var app = builder.Build();

_ = app.Services.GetRequiredService<SqliteConnection>();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
    await db.Database.MigrateAsync();
}

app.MapOpenApi();
app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseMiddleware<IpRateLimitMiddleware>();

// Map endpoints
app.MapPost("/transactions", CreateTransaction)
    .WithName("CreateTransaction")
    .Produces<Guid>(201)
    .Produces(400);

app.MapGet("/transactions", GetTransactions)
    .WithName("GetTransactions")
    .WithOpenApi()
    .Produces<List<object>>(200);

app.MapGet("/transactions/export", ExportTransactionsCsv)
    .WithName("ExportTransactionsCsv")
    .Produces(200)
    .Produces(400);

app.MapGet("/transactions/{id}", GetTransactionById)
    .WithName("GetTransactionById")
    .Produces<object>(200)
    .Produces(404);

app.MapGet("/accounts/{accountId}/balance", GetAccountBalance)
    .WithName("GetAccountBalance")
    .Produces<decimal>(200);

app.MapGet("/accounts/{accountId}/summary", GetAccountSummary)
    .WithName("GetAccountSummary")
    .Produces<object>(200);

app.MapGet("/accounts/{accountId}/interest", GetAccountInterest)
    .WithName("GetAccountInterest")
    .Produces<object>(200)
    .Produces(400);

// Scalar API documentation
app.MapScalarApiReference();

await app.RunAsync();

// Endpoint handlers
async Task<IResult> CreateTransaction(CreateTransactionCommand command, IMediator mediator)
{
    try
    {
        var id = await mediator.Send(command);
        return Results.Created($"/transactions/{id}", new { id });
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new
        {
            error = "Validation failed",
            details = ex.Errors.Select(e => new
            {
                field = e.PropertyName,
                message = e.ErrorMessage
            })
        });
    }
}

async Task<IResult> GetTransactions(string? accountId, string? type, DateTime? from, DateTime? to, IMediator mediator)
{
    var query = new GetTransactionsByFilterQuery
    {
        AccountId = accountId,
        Type = type,
        FromDate = from,
        ToDate = to
    };

    var transactions = await mediator.Send(query);
    return Results.Ok(transactions);
}

async Task<IResult> ExportTransactionsCsv(string? format, IMediator mediator)
{
    if (string.IsNullOrWhiteSpace(format))
        return Results.BadRequest(new { error = "The \"format\" query parameter is required." });

    if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Unsupported export format; only \"csv\" is supported." });

    var csv = await mediator.Send(new ExportTransactionsCsvQuery());
    return Results.Text(csv, "text/csv; charset=utf-8", Encoding.UTF8);
}

async Task<IResult> GetTransactionById(Guid id, IMediator mediator)
{
    var transaction = await mediator.Send(new GetTransactionByIdQuery(id));
    if (transaction == null)
        return Results.NotFound(new { error = "Transaction not found" });

    return Results.Ok(transaction);
}

async Task<IResult> GetAccountBalance(string accountId, IMediator mediator)
{
    var balance = await mediator.Send(new GetAccountBalanceQuery(accountId));
    return Results.Ok(new { accountId, balance });
}

async Task<IResult> GetAccountSummary(string accountId, IMediator mediator)
{
    var summary = await mediator.Send(new GetAccountSummaryQuery(accountId));
    return Results.Ok(new
    {
        accountId,
        summary.TotalDeposits,
        summary.TotalWithdrawals,
        summary.TransactionCount,
        summary.MostRecentTransactionDate
    });
}

async Task<IResult> GetAccountInterest(string accountId, decimal rate, int days, IMediator mediator)
{
    try
    {
        var result = await mediator.Send(new GetAccountInterestQuery(accountId, rate, days));
        return Results.Ok(new
        {
            accountId = result.AccountId,
            principal = result.Principal,
            rate = result.Rate,
            days = result.Days,
            interest = result.Interest
        });
    }
    catch (InvalidInterestParametersException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}

// Validation behavior for MediatR pipeline
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
