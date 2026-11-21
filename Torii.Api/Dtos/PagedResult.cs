namespace Torii.Api.Dtos;

/// <summary>One page of a list (<see cref="Page"/> is 1-based).</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
