using ECommerce.DTOs.Auth;
using ECommerce.Extensions;
using ECommerce.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct) =>
        Respond(await users.GetProfileAsync(User.GetUserId(), ct));

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request, CancellationToken ct) =>
        Respond(await users.UpdateProfileAsync(User.GetUserId(), request, ct));

    [HttpPost("me/deactivate")]
    public async Task<IActionResult> Deactivate(DeactivateAccountRequest request, CancellationToken ct) =>
        Respond(await users.DeactivateAsync(User.GetUserId(), request, ct));
}
