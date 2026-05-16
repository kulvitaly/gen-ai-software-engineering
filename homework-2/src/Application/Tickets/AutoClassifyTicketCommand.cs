using Application.Common;
using MediatR;

namespace Application.Tickets;

public sealed record AutoClassifyTicketCommand(Guid Id) : IRequest<ApplicationResult<ClassificationDto>>;
