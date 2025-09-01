using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Server.Services;

namespace Server.Middleware;

public class ApiKeyGuardAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var store = context.HttpContext.RequestServices.GetRequiredService<ISecretStore>();
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ApiKeyGuardAttribute>>();
        if (!await store.HasApiKeyAsync())
        {
            logger.LogWarning("GW2 API key missing");
            context.Result = new JsonResult(new
            {
                error = "MissingApiKey",
                message = "Guild Wars 2 API key not configured",
                howTo = new[] { "Open Web UI", "Paste key", "Save" }
            }) { StatusCode = 400 };
            return;
        }
        await next();
    }
}

