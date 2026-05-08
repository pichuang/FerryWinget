namespace FerryWinget.Server.Controllers;

using FerryWinget.Core.Configuration;
using FerryWinget.Server.Models;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public sealed class InformationController : ControllerBase
{
    private readonly ServerConfig _config;

    public InformationController(ServerConfig config)
    {
        _config = config;
    }

    [HttpGet("information")]
    public ActionResult<InformationResponse> GetInformation()
    {
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
