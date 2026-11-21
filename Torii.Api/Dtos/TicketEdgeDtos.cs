using System.ComponentModel.DataAnnotations;

namespace Torii.Api.Dtos;

/// <summary>Ticket routing rule: the source customer may send tickets to the target customer.</summary>
public record TicketEdgeDto(
    long Id,
    long SourceCustomerId,
    string SourceCustomerName,
    long TargetCustomerId,
    string TargetCustomerName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record TicketEdgeCreateRequest(
    [Range(1, long.MaxValue)] long SourceCustomerId,
    [Range(1, long.MaxValue)] long TargetCustomerId);

public record TicketEdgeUpdateRequest(
    [Range(1, long.MaxValue)] long SourceCustomerId,
    [Range(1, long.MaxValue)] long TargetCustomerId,
    bool IsActive);
