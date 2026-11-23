using Microsoft.EntityFrameworkCore;
using Torii.Api.Data;
using Torii.Api.Dtos;
using Torii.Api.Exceptions;
using Torii.Api.Models;

namespace Torii.Api.Managers;

/// <summary>
/// Review of the accounts requested through /register (PENDING). Approving activates the
/// account; declining marks it REJECTED (it stays disabled and keeps its email). Either way
/// the request leaves the list and its notifications are removed.
/// </summary>
public class AccountRequestManager(
    ToriiDbContext db,
    NotificationManager notifications,
    ILogger<AccountRequestManager> logger) : ToriManager(db)
{
    private IQueryable<User> Pending => _dbContext.Users
        .Where(u => u.Status == AccountStatus.PENDING && !u.IsDeleted);

    /// <summary>Pending requests, oldest first (first come, first served).</summary>
    public async Task<IReadOnlyList<AccountRequestDto>> ListAsync(CancellationToken ct = default)
        => await Pending
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
            .Select(u => new AccountRequestDto(u.Id, u.Email, u.DisplayName, u.Position, u.AvatarUrl, u.AccountType, u.CreatedAt))
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => Pending.CountAsync(ct);

    public Task ApproveAsync(long id, long actorId, CancellationToken ct = default)
        => DecideAsync(id, actorId, AccountStatus.ACTIVE, ct);

    public Task RejectAsync(long id, long actorId, CancellationToken ct = default)
        => DecideAsync(id, actorId, AccountStatus.REJECTED, ct);

    private async Task DecideAsync(long id, long actorId, AccountStatus decision, CancellationToken ct)
    {
        // 404 as well when another administrator already decided: the request is gone.
        var user = await Pending.FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw NotFoundException.For("Account request", id);

        user.Status = decision;
        user.IsActive = decision == AccountStatus.ACTIVE;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedById = actorId;
        await _dbContext.SaveChangesAsync(ct);
        await notifications.RemoveAccountRequestAsync(user.Id, ct);

        logger.LogInformation("Account request {Decision}: {Email} (user {UserId}) by user {ActorId}.",
            decision == AccountStatus.ACTIVE ? "approved" : "declined", user.Email, user.Id, actorId);
    }
}
