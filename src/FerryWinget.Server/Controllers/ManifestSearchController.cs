namespace FerryWinget.Server.Controllers;

using FerryWinget.Server.Models;
using FerryWinget.Server.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public sealed class ManifestSearchController : ControllerBase
{
    private readonly SearchService _searchService;
    private readonly ILogger<ManifestSearchController> _logger;

    public ManifestSearchController(SearchService searchService, ILogger<ManifestSearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    [HttpPost("manifestSearch")]
    public ActionResult<ManifestSearchResponse> Search([FromBody] ManifestSearchRequest request)
    {
        _logger.LogDebug("POST /api/manifestSearch — Query: {Query}", request.Query?.KeyWord);
        var result = _searchService.Search(request);
        _logger.LogDebug("Search returned {Count} results", result.Data.Count);
        return Ok(result);
    }
}
