using ECommerce.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Tests.Infrastructure;

/// <summary>Test-only endpoints, one per authorization policy. Not part of the shipped API.</summary>
[ApiController]
[Route("api/v1/_probe")]
public class ProbeController : ControllerBase
{
    [Authorize(Policy = AuthPolicies.VerifiedUser)]
    [HttpGet("verified")]
    public IActionResult Verified() => Ok();

    [Authorize(Policy = AuthPolicies.PlatformAdmin)]
    [HttpGet("platform-admin")]
    public IActionResult PlatformAdmin() => Ok();
}
