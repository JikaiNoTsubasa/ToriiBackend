using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Exceptions;

namespace Torii.Api.Controllers;

[ApiController]
public class ToriController : ControllerBase
{
    /// <summary>
    /// Id of the signed-in user, read from the JWT (sub claim).
    /// Only use on <c>[Authorize]</c> actions.
    /// </summary>
    protected long CurrentUserId
    {
        get
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return long.TryParse(id, out var userId)
                ? userId
                : throw new AuthenticationException("User could not be identified.");
        }
    }
}
