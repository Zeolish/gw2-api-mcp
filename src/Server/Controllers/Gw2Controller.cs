using Microsoft.AspNetCore.Mvc;
using Server.Middleware;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/gw2")]
[ApiKeyGuard]
public class Gw2Controller : ControllerBase
{
    private readonly Gw2ProxyService _proxy;

    public Gw2Controller(Gw2ProxyService proxy)
    {
        _proxy = proxy;
    }

    [HttpGet("{**path}")]
    public async Task<IActionResult> Get(string path)
    {
        var result = await _proxy.GetAsync(path, Request.QueryString.Value);
        return new ContentResult
        {
            Content = result.Body,
            ContentType = result.ContentType,
            StatusCode = (int)result.StatusCode
        };
    }
}

