using Microsoft.AspNetCore.Mvc;

namespace FUEN42Team3.Frontend.WebApi.Controllers
{
    [ApiController]
    public class HealthController : ControllerBase
    {
        [HttpGet("/health")]
        public IActionResult Health() => Ok(new { status = "ok", time = DateTime.UtcNow });

        [HttpGet("/api/ping")]
        public IActionResult Ping() => Ok("pong");
    }
}
