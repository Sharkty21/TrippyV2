using Microsoft.AspNetCore.Mvc;
using Trippy.Backend.DTOs;

namespace Trippy.Backend.Controllers;

[ApiController]
[Route("api")]
[Tags("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("health", Name = "health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public HealthResponse Get() => new();
}
