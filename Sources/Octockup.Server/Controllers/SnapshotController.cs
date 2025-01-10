using Microsoft.AspNetCore.Mvc;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class SnapshotController : ControllerBase
    {
        [HttpGet]
        [Route(Routes.Version + "/[controller]")]
        public IActionResult GetSnapshots()
        {
            return Ok();
        }
    }
}
