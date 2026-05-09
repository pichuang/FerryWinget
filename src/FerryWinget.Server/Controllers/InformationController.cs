namespace FerryWinget.Server.Controllers;

using FerryWinget.Core.Configuration;
using FerryWinget.Server.Models;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public sealed class InformationController : ControllerBase
{
    private readonly ServerConfig _config;
    private readonly ILogger<InformationController> _logger;

    public InformationController(ServerConfig config, ILogger<InformationController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpGet("information")]
    [ResponseCache(Duration = 300)]
    public ActionResult<InformationResponse> GetInformation()
    {
        _logger.LogDebug("GET /api/information");
        return Ok(new InformationResponse
        {
            Data = new InformationData
            {
                SourceIdentifier = _config.SourceIdentifier,
                ServerSupportedVersions = _config.SupportedApiVersions
            }
        });
    }
}
