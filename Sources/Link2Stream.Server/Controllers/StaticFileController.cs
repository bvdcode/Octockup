using Microsoft.AspNetCore.Mvc;
using Link2Stream.Server.Resources;

namespace Link2Stream.Server.Controllers
{
    [ApiController]
    public class StaticFileController : ControllerBase
    {
        [HttpGet("/favicon.ico")]
        public IActionResult GetFavicon()
        {
            return File(AppResources.Favicon, "image/x-icon");
        }

        [HttpGet("/upload")]
        public IActionResult GetUploadPage()
        {
            return Ok("<html></html>");
        }
    }
}
