using ECommerce.DTOs.Generic;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/v1/system")]
public class SystemController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() =>
        Ok(ApiResponse.Success("pong", new { service = "ecommerce-api", utcNow = DateTime.UtcNow }));
}
