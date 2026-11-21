using Microsoft.EntityFrameworkCore;
using Torii.Api.Data;
using Torii.Api.Dtos;
using Torii.Api.Exceptions;
using Torii.Api.Models;

namespace Torii.Api.Managers;

/// <summary>
/// Administration of customers and their members. Logical deletion (soft delete):
/// a deleted customer disappears from the lists and no longer grants access to tickets.
/// </summary>
public class CustomerManager(ToriiDbContext db) : ToriManager(db)
{
    private IQueryable<Customer> Active => _dbContext.Customers.Where(c => !c.IsDeleted);

    public async Task<IReadOnlyList<CustomerListItemDto>> ListAsync(CancellationToken ct = default)
        => await Active
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CustomerListItemDto(c.Id, c.Name, c.Members.Count, c.CreatedAt))
            .ToListAsync(ct);

    public async Task<CustomerDetailDto> GetAsync(long id, CancellationToken ct = default)
    {
        var customer = await Active
            .AsNoTracking()
            .Include(c => c.Members.OrderBy(u => u.DisplayName))
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw NotFoundException.For("Customer", id);

        return ToDetail(customer);
    }

    public async Task<CustomerDetailDto> CreateAsync(CustomerUpsertRequest request, long actorId, CancellationToken ct = default)
    {
        var customer = new Customer
        {
            Name = NormalizeName(request.Name),
            CreatedById = actorId,
            UpdatedAt = null,
        };
        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(ct);

        return ToDetail(customer);
    }

    public async Task<CustomerDetailDto> RenameAsync(long id, CustomerUpsertRequest request, long actorId, CancellationToken ct = default)
    {
        var customer = await FindTrackedAsync(id, ct);
        customer.Name = NormalizeName(request.Name);
        Touch(customer, actorId);
        await _dbContext.SaveChangesAsync(ct);

        return ToDetail(customer);
    }

    public async Task DeleteAsync(long id, long actorId, CancellationToken ct = default)
    {
        var customer = await FindTrackedAsync(id, ct);
        customer.IsDeleted = true;
        customer.DeletedAt = DateTime.UtcNow;
        customer.DeletedById = actorId;
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<CustomerDetailDto> AddMemberAsync(long id, long userId, long actorId, CancellationToken ct = default)
    {
        var customer = await FindTrackedAsync(id, ct);

        if (customer.Members.Any(u => u.Id == userId))
            throw new ConflictException("This user is already a member of the customer.");

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted, ct)
                   ?? throw NotFoundException.For("User", userId);

        customer.Members.Add(user);
        Touch(customer, actorId);
        await _dbContext.SaveChangesAsync(ct);

        return ToDetail(customer);
    }

    public async Task<CustomerDetailDto> RemoveMemberAsync(long id, long userId, long actorId, CancellationToken ct = default)
    {
        var customer = await FindTrackedAsync(id, ct);
        var member = customer.Members.FirstOrDefault(u => u.Id == userId)
                     ?? throw new NotFoundException("This user is not a member of the customer.");

        customer.Members.Remove(member);
        Touch(customer, actorId);
        await _dbContext.SaveChangesAsync(ct);

        return ToDetail(customer);
    }

    private async Task<Customer> FindTrackedAsync(long id, CancellationToken ct)
        => await Active
               .Include(c => c.Members)
               .FirstOrDefaultAsync(c => c.Id == id, ct)
           ?? throw NotFoundException.For("Customer", id);

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length > 0
            ? trimmed
            : throw new ValidationException(
                new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
    }

    private static void Touch(Customer customer, long actorId)
    {
        customer.UpdatedAt = DateTime.UtcNow;
        customer.UpdatedById = actorId;
    }

    private static CustomerDetailDto ToDetail(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.CreatedAt,
        customer.UpdatedAt,
        customer.Members.OrderBy(u => u.DisplayName).Select(UserDto.From).ToList());
}
