namespace Torii.Api.Models;

public class Customer : Entity
{
    public string Name { get; set; } = null!;

    /// <summary>Member users (N-N through the CustomerMembers table).</summary>
    public ICollection<User> Members { get; set; } = [];

    /// <summary>Ticket channels this customer takes part in.</summary>
    public ICollection<ChannelParticipant> ChannelParticipations { get; set; } = [];
}
