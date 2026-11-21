namespace Torii.Api.Models;

/// <summary>
/// Ticket routing rule: <see cref="SourceCustomer"/> may send tickets to
/// <see cref="TargetCustomer"/>. Directed: A → B does not allow B → A (add the
/// reverse edge for that). Only one live edge per (source, target) pair, and a
/// customer cannot target itself (see ToriiDbContext).
/// </summary>
public class TicketEdge : Entity
{
    public long SourceCustomerId { get; set; }
    public Customer SourceCustomer { get; set; } = null!;

    public long TargetCustomerId { get; set; }
    public Customer TargetCustomer { get; set; } = null!;
}
