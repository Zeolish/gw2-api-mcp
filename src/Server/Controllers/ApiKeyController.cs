using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApiKeyController : ControllerBase
{
    private readonly ISecretStore _store;

    public ApiKeyController(ISecretStore store)
    {
        _store = store;
    }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(new { hasApiKey = await _store.HasApiKeyAsync() });

    public record ApiKeyRequest(string Key);

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ApiKeyRequest request)
    {
        await _store.SaveApiKeyAsync(request.Key);
        return Ok(new { hasApiKey = true });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        await _store.DeleteApiKeyAsync();
        return Ok(new { hasApiKey = false });
    }
}

