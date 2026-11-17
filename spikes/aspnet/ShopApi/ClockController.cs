using Microsoft.AspNetCore.Mvc;

namespace ShopApi;

[ApiController]
[Route("clock")]
public sealed class ClockController(IClock clock) : ControllerBase
{
    [HttpGet]
    public DateTime Get() => clock.Now;
}
