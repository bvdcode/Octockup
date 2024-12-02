using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class LinkController : ControllerBase
    {
        [HttpGet("/l/{linkId}")]
        public IActionResult GetShortLink([FromRoute][Required] string linkId)
        {
            return GetLink(linkId);
        }

        [HttpGet("/link/{linkId}")]
        public IActionResult GetLink([FromRoute][Required] string linkId)
        {
            return Ok(linkId);
        }

        [HttpPost("/link")]
        public IActionResult CreateLink()
        {
            return Ok("Created");
        }
    }
}
