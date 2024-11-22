using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Link2Stream.Server.Controllers
{
    [ApiController]
    public class LinkController : ControllerBase
    {

        [HttpGet("/link/upload")]
        public IActionResult GetUploadPage()
        {
            return Ok("<html></html>");
        }

        [HttpGet("/link/{linkId}")]
        public IActionResult GetLink([FromRoute][Required] string linkId)
        {
            return Ok(linkId);
        }

        [HttpPost("/link")]
        public IActionResult CreateLink([FromBody][Required] string url)
        {
            return Ok("Created");
        }
    }
}
