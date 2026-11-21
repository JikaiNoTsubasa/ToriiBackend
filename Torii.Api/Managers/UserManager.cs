using Microsoft.EntityFrameworkCore;
using Torii.Api.Data;
using Torii.Api.Dtos;

namespace Torii.Api.Managers;

public class UserManager(ToriiDbContext db) : ToriManager(db)
{
    private const int MaxResults = 20;

    /// <summary>
    /// Searches active users by email or display name (case-insensitive).
    /// Without a term, returns the first users in alphabetical order.
    /// </summary>
    public async Task<IReadOnlyList<UserDto>> SearchAsync(string? search, CancellationToken ct = default)
    {
        var query = _dbContext.Users.AsNoTracking().Where(u => u.IsActive && !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search.Trim())}%";
            query = query.Where(u => EF.Functions.ILike(u.Email, pattern, "\\")
                                  || EF.Functions.ILike(u.DisplayName, pattern, "\\"));
        }

        var users = await query.OrderBy(u => u.DisplayName).Take(MaxResults).ToListAsync(ct);
        return users.Select(UserDto.From).ToList();
    }

    /// <summary>Escapes LIKE wildcards typed by the user.</summary>
    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
