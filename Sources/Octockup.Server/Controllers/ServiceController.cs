using System.Reflection;
using EasyExtensions.Models;
using EasyExtensions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class ServiceController(CpuUsageService service) : ControllerBase
    {
        private readonly CpuUsageService service = service;

        [Produces("application/json")]
        [HttpGet(Routes.Service.Health)]
        public IActionResult Health()
        {
            return Ok();
        }

        [HttpGet(Routes.Service.Time)]
        [Produces("application/json", Type = typeof(CurrentTimeUtc))]
        public IActionResult Time()
        {
            return Ok(new CurrentTimeUtc());
        }

        [HttpGet(Routes.Service.AppVersion)]
        [Produces("application/json", Type = typeof(Version))]
        public IActionResult Version()
        {
            var responce = new
            {
                Assembly.GetExecutingAssembly().GetName().Version
            };
            return Ok(responce);
        }

        [HttpGet(Routes.Service.Metrics)]
        [Produces("application/json", Type = typeof(UsageReport))]
        public IActionResult Metrics()
        {
            var result = service.GetUsage();
            return Ok(result);
        }

        [HttpGet(Routes.Service.Headers)]
        public Dictionary<string, StringValues> CheckHeaders()
        {
            return Request.Headers.ToDictionary();
        }
    }
}
