namespace FerryWinget.Server.Controllers;

using FerryWinget.Server.Models;
using FerryWinget.Server.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public sealed class ManifestSearchController : ControllerBase
{
    private readonly SearchService _searchService;

    public ManifestSearchController(SearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpPost("manifestSearch")]
    public ActionResult<ManifestSearchResponse> Search([FromBody] ManifestSearchRequest request)
    {
        var result = _searchService.Search(request);
        return Ok(result);
    }
}
