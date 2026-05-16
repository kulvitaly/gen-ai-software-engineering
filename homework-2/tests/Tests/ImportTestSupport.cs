using Application.Common;
using Application.Tickets;
using Domain.Tickets;

namespace Tests;

internal sealed class ImportTestClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

internal sealed class ImportTestRepository : ITicketRepository
{
    private readonly List<Ticket> _tickets = [];

    public IReadOnlyList<Ticket> Tickets => _tickets;

    public Task Initialize(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task Add(Ticket ticket, CancellationToken cancellationToken = default)
    {
        _tickets.Add(ticket);
        return Task.CompletedTask;
    }

    public Task<Ticket?> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tickets.SingleOrDefault(ticket => ticket.Id == id));
    }

    public Task<IReadOnlyList<Ticket>> List(TicketFilter filter, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Ticket>>(_tickets);
    }

    public Task<bool> Update(Ticket ticket, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}

internal static class ImportFixtures
{
    public const string CsvHeader = "customer_id,customer_email,customer_name,subject,description,category,priority,status,tags,metadata.source,metadata.browser,metadata.device_type,assigned_to";

    public static string ValidCsv()
    {
        return string.Join(
            Environment.NewLine,
            CsvHeader,
            "customer-1,ada@example.com,Ada Lovelace,Cannot access account,I cannot access my customer account after resetting my password.,account_access,high,new,account;login,web_form,Edge,desktop,agent-1",
            "customer-2,grace@example.com,Grace Hopper,Billing invoice question,I need help understanding the latest annual invoice.,billing_question,medium,new,billing;invoice,email,Firefox,desktop,");
    }

    public static string ValidJsonArray()
    {
        return """
        [
          {
            "customer_id": "customer-1",
            "customer_email": "ada@example.com",
            "customer_name": "Ada Lovelace",
            "subject": "Cannot access account",
            "description": "I cannot access my customer account after resetting my password.",
            "category": "account_access",
            "priority": "high",
            "status": "new",
            "tags": ["account", "login"],
            "metadata": { "source": "web_form", "browser": "Edge", "device_type": "desktop" }
          }
        ]
        """;
    }

    public static string ValidXml()
    {
        return """
        <tickets>
          <ticket>
            <customer_id>customer-1</customer_id>
            <customer_email>ada@example.com</customer_email>
            <customer_name>Ada Lovelace</customer_name>
            <subject>Cannot access account</subject>
            <description>I cannot access my customer account after resetting my password.</description>
            <category>account_access</category>
            <priority>high</priority>
            <status>new</status>
            <tags><tag>account</tag><tag>login</tag></tags>
            <metadata>
              <source>web_form</source>
              <browser>Edge</browser>
              <device_type>desktop</device_type>
            </metadata>
          </ticket>
        </tickets>
        """;
    }
}
