using ECommerce.DTOs.Generic;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult Respond(ApiResponse response) => StatusCode(response.HttpStatusCode, response);

    protected string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
