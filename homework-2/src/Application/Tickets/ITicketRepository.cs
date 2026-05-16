using Domain.Tickets;

namespace Application.Tickets;

public interface ITicketRepository
{
    Task Initialize(CancellationToken cancellationToken = default);

    Task Add(Ticket ticket, CancellationToken cancellationToken = default);

    Task<Ticket?> GetById(Guid id, CancellationToken cancellationToken = default);

    Task<bool> Update(Ticket ticket, CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid id, CancellationToken cancellationToken = default);
}
