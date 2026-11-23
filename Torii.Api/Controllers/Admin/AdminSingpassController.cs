using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Dtos;
using Torii.Api.Managers;
using Torii.Api.Models;

namespace Torii.Api.Controllers.Admin;

/// <summary>Singpass settings (ADMIN only).</summary>
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/admin/singpass")]
public class AdminSingpassController(SingpassConfigManager configs) : ToriController
{
    [HttpGet]
    public async Task<ActionResult<SingpassConfigDto>> Get(CancellationToken ct)
        => Ok(await configs.GetDtoAsync(ct));

    /// <summary>Saves the settings; enabling requires the client id, redirect URI and keys (400 otherwise).</summary>
    [HttpPut]
    public async Task<ActionResult<SingpassConfigDto>> Update(SingpassConfigUpdateRequest request, CancellationToken ct)
        => Ok(await configs.UpdateAsync(request, CurrentUserId, ct));

    /// <summary>Generates new signing and encryption keys (the previous ones stop working).</summary>
    [HttpPost("keys")]
    public async Task<ActionResult<SingpassConfigDto>> GenerateKeys(CancellationToken ct)
        => Ok(await configs.GenerateKeysAsync(CurrentUserId, ct));

    /// <summary>Reads the Singpass discovery document of the selected environment.</summary>
    [HttpPost("test")]
    public async Task<ActionResult<SingpassTestResultDto>> Test(CancellationToken ct)
        => Ok(await configs.TestAsync(ct));
}
