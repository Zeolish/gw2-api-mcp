using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly ISecretStore _store;

    public StatusController(ISecretStore store)
    {
        _store = store;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var port = 5123;
        var envPort = Environment.GetEnvironmentVariable("PORT");
        if (envPort != null && int.TryParse(envPort, out var p))
            port = p;
        else
        {
            var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            if (urls != null)
            {
                var match = System.Text.RegularExpressions.Regex.Match(urls, @":(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var up))
                    port = up;
            }
        }

        return Ok(new
        {
            server = "running",
            port,
            hasApiKey = await _store.HasApiKeyAsync()
        });
    }
}

