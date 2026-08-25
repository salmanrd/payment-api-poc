using Microsoft.AspNetCore.Mvc;

namespace PaymentApi.Controllers;

[ApiController]
[Tags("Operations")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get() => Ok(new { status = "UP" });
}
