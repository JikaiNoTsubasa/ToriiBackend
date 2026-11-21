using Microsoft.EntityFrameworkCore;
using Torii.Api.Data;
using Torii.Api.Dtos;
using Torii.Api.Exceptions;
using Torii.Api.Models;

namespace Torii.Api.Managers;

/// <summary>
/// Ticket configuration: edges stating which customer may send tickets to which
/// other customer. Deletion is a soft delete. Edges whose source or target customer
/// was deleted are hidden and never allow sending.
/// </summary>
public class TicketEdgeManager(ToriiDbContext db) : ToriManager(db)
{
    /// <summary>Live edges (not deleted, both customers still present).</summary>
    private IQueryable<TicketEdge> Live => _dbContext.TicketEdges.Where(e =>
        !e.IsDeleted && !e.SourceCustomer.IsDeleted && !e.TargetCustomer.IsDeleted);

    public async Task<IReadOnlyList<TicketEdgeDto>> ListAsync(CancellationToken ct = default)
        => await Live
            .AsNoTracking()
            .OrderBy(e => e.SourceCustomer.Name)
            .ThenBy(e => e.TargetCustomer.Name)
            .Select(e => new TicketEdgeDto(
                e.Id,
                e.SourceCustomerId, e.SourceCustomer.Name,
                e.TargetCustomerId, e.TargetCustomer.Name,
                e.IsActive, e.CreatedAt, e.UpdatedAt))
            .ToListAsync(ct);

    /// <summary>
    /// Whether <paramref name="sourceCustomerId"/> may send tickets to
    /// <paramref name="targetCustomerId"/> (an active live edge exists).
    /// To use when creating tickets.
    /// </summary>
    public Task<bool> CanSendAsync(long sourceCustomerId, long targetCustomerId, CancellationToken ct = default)
        => Live.AnyAsync(e => e.IsActive
                              && e.SourceCustomerId == sourceCustomerId
                              && e.TargetCustomerId == targetCustomerId, ct);

    public async Task<TicketEdgeDto> CreateAsync(TicketEdgeCreateRequest request, long actorId, CancellationToken ct = default)
    {
        await ValidatePairAsync(request.SourceCustomerId, request.TargetCustomerId, excludedEdgeId: null, ct);

        var edge = new TicketEdge
        {
            SourceCustomerId = request.SourceCustomerId,
            TargetCustomerId = request.TargetCustomerId,
            CreatedById = actorId,
            UpdatedAt = null,
        };
        _dbContext.TicketEdges.Add(edge);
        await _dbContext.SaveChangesAsync(ct);

        return await GetDtoAsync(edge.Id, ct);
    }

    public async Task<TicketEdgeDto> UpdateAsync(long id, TicketEdgeUpdateRequest request, long actorId, CancellationToken ct = default)
    {
        var edge = await FindTrackedAsync(id, ct);
        await ValidatePairAsync(request.SourceCustomerId, request.TargetCustomerId, excludedEdgeId: id, ct);

        edge.SourceCustomerId = request.SourceCustomerId;
        edge.TargetCustomerId = request.TargetCustomerId;
        edge.IsActive = request.IsActive;
        edge.UpdatedAt = DateTime.UtcNow;
        edge.UpdatedById = actorId;
        await _dbContext.SaveChangesAsync(ct);

        return await GetDtoAsync(id, ct);
    }

    public async Task DeleteAsync(long id, long actorId, CancellationToken ct = default)
    {
        var edge = await FindTrackedAsync(id, ct);
        edge.IsDeleted = true;
        edge.DeletedAt = DateTime.UtcNow;
        edge.DeletedById = actorId;
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task ValidatePairAsync(long sourceId, long targetId, long? excludedEdgeId, CancellationToken ct)
    {
        if (sourceId == targetId)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["targetCustomerId"] = ["The target customer must differ from the source customer."],
            });

        var existing = await _dbContext.Customers
            .Where(c => !c.IsDeleted && (c.Id == sourceId || c.Id == targetId))
            .Select(c => c.Id)
            .ToListAsync(ct);
        if (!existing.Contains(sourceId))
            throw NotFoundException.For("Customer", sourceId);
        if (!existing.Contains(targetId))
            throw NotFoundException.For("Customer", targetId);

        var duplicate = await _dbContext.TicketEdges.AnyAsync(e =>
            !e.IsDeleted && e.Id != excludedEdgeId
            && e.SourceCustomerId == sourceId && e.TargetCustomerId == targetId, ct);
        if (duplicate)
            throw new ConflictException("An edge already exists between these customers in this direction.");
    }

    private async Task<TicketEdge> FindTrackedAsync(long id, CancellationToken ct)
        => await Live.FirstOrDefaultAsync(e => e.Id == id, ct)
           ?? throw NotFoundException.For("Ticket edge", id);

    private async Task<TicketEdgeDto> GetDtoAsync(long id, CancellationToken ct)
        => await _dbContext.TicketEdges
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new TicketEdgeDto(
                e.Id,
                e.SourceCustomerId, e.SourceCustomer.Name,
                e.TargetCustomerId, e.TargetCustomer.Name,
                e.IsActive, e.CreatedAt, e.UpdatedAt))
            .FirstAsync(ct);
}
