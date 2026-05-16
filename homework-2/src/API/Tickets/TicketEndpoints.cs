using Application.Common;
using Application.Tickets;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace API.Tickets;

internal static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tickets")
            .WithTags("Tickets");

        group.MapPost("/", Create)
            .WithName("CreateTicket");
        group.MapGet("/", List)
            .WithName("ListTickets");
        group.MapGet("/{id:guid}", GetById)
            .WithName("GetTicket");
        group.MapPut("/{id:guid}", Update)
            .WithName("UpdateTicket");
        group.MapDelete("/{id:guid}", Delete)
            .WithName("DeleteTicket");

        return endpoints;
    }

    private static async Task<Results<Created<TicketResponse>, ValidationProblem>> Create(
        CreateTicketRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!ApiValidation.TryValidate(request, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await sender.Send(request.ToCommand(), cancellationToken);
        if (result.Status == ApplicationResultStatus.ValidationError)
        {
            return ApiValidation.FromErrors(result.Errors);
        }

        var response = result.Value!.ToResponse();
        return TypedResults.Created($"/tickets/{response.Id}", response);
    }

    private static async Task<Results<Ok<IReadOnlyList<TicketResponse>>, ValidationProblem>> List(
        string? category,
        string? priority,
        string? status,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var errors = ValidateFilters(category, priority, status);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await sender.Send(
            new ListTicketsQuery(category.ToCategory(), priority.ToPriority(), status.ToStatus()),
            cancellationToken);

        if (result.Status == ApplicationResultStatus.ValidationError)
        {
            return ApiValidation.FromErrors(result.Errors);
        }

        return TypedResults.Ok((IReadOnlyList<TicketResponse>)result.Value!.Select(ticket => ticket.ToResponse()).ToArray());
    }

    private static async Task<Results<Ok<TicketResponse>, NotFound, ValidationProblem>> GetById(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTicketQuery(id), cancellationToken);

        return result.Status switch
        {
            ApplicationResultStatus.Success => TypedResults.Ok(result.Value!.ToResponse()),
            ApplicationResultStatus.NotFound => TypedResults.NotFound(),
            _ => ApiValidation.FromErrors(result.Errors)
        };
    }

    private static async Task<Results<Ok<TicketResponse>, NotFound, ValidationProblem>> Update(
        Guid id,
        UpdateTicketRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!ApiValidation.TryValidate(request, out var errors))
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await sender.Send(request.ToCommand(id), cancellationToken);

        return result.Status switch
        {
            ApplicationResultStatus.Success => TypedResults.Ok(result.Value!.ToResponse()),
            ApplicationResultStatus.NotFound => TypedResults.NotFound(),
            _ => ApiValidation.FromErrors(result.Errors)
        };
    }

    private static async Task<Results<Ok<DeleteTicketApiResponse>, NotFound, ValidationProblem>> Delete(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteTicketCommand(id), cancellationToken);

        return result.Status switch
        {
            ApplicationResultStatus.Success => TypedResults.Ok(new DeleteTicketApiResponse(result.Value!.Id)),
            ApplicationResultStatus.NotFound => TypedResults.NotFound(),
            _ => ApiValidation.FromErrors(result.Errors)
        };
    }

    private static Dictionary<string, string[]> ValidateFilters(string? category, string? priority, string? status)
    {
        var errors = new Dictionary<string, string[]>();
        if (category is not null && !EnumParser.TryParseCategory(category, out _))
        {
            errors["category"] = ["Category must be a supported ticket category."];
        }

        if (priority is not null && !EnumParser.TryParsePriority(priority, out _))
        {
            errors["priority"] = ["Priority must be a supported ticket priority."];
        }

        if (status is not null && !EnumParser.TryParseStatus(status, out _))
        {
            errors["status"] = ["Status must be a supported ticket status."];
        }

        return errors;
    }
}
