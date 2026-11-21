using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Managers;
using Torii.Api.Models;

namespace Torii.Api.Controllers.Admin;

[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/admin/customers")]
public class AdminCustomerController(CustomerManager customers) : ToriController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerListItemDto>>> List(CancellationToken ct)
        => Ok(await customers.ListAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CustomerDetailDto>> Get(long id, CancellationToken ct)
        => Ok(await customers.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<CustomerDetailDto>> Create(CustomerUpsertRequest request, CancellationToken ct)
    {
        var created = await customers.CreateAsync(request, CurrentUserId, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<CustomerDetailDto>> Rename(long id, CustomerUpsertRequest request, CancellationToken ct)
        => Ok(await customers.RenameAsync(id, request, CurrentUserId, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await customers.DeleteAsync(id, CurrentUserId, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/members")]
    public async Task<ActionResult<CustomerDetailDto>> AddMember(long id, AddCustomerMemberRequest request, CancellationToken ct)
        => Ok(await customers.AddMemberAsync(id, request.UserId, CurrentUserId, ct));

    [HttpDelete("{id:long}/members/{userId:long}")]
    public async Task<ActionResult<CustomerDetailDto>> RemoveMember(long id, long userId, CancellationToken ct)
        => Ok(await customers.RemoveMemberAsync(id, userId, CurrentUserId, ct));
}
