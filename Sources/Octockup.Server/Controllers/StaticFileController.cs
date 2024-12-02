using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Resources;

namespace Octockup.Server.Controllers
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
