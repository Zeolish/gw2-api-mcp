using Microsoft.AspNetCore.Http.Json;
using Polly;
using Polly.Extensions.Http;
using Server.Services;
using Server.Mcp;
using System.Net;
using System.Net.Http;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<ISecretStore, SecretStore>();

builder.Services.AddHttpClient("gw2", c =>
{
    c.BaseAddress = new Uri("https://api.guildwars2.com/");
}).AddPolicyHandler(GetRetryPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * attempt));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapGet("/api/status", async (ISecretStore store, IHttpContextAccessor accessor) => new
{
    server = Environment.MachineName,
    port = accessor.HttpContext?.Connection.LocalPort,
    hasApiKey = await store.HasApiKeyAsync()
});

app.MapGet("/api/apikey", async (ISecretStore store) => new { hasApiKey = await store.HasApiKeyAsync() });

app.MapPost("/api/apikey", async (ISecretStore store, ApiKeyDto dto) =>
{
    await store.SaveApiKeyAsync(dto.Key);
    return Results.Ok();
});

app.MapDelete("/api/apikey", async (ISecretStore store) =>
{
    await store.DeleteApiKeyAsync();
    return Results.Ok();
});

var gw2 = app.MapGroup("/api/gw2");

gw2.MapGet("/{**path}", async (string path, HttpContext ctx, ISecretStore store, IHttpClientFactory factory) =>
{
    var key = await store.GetApiKeyAsync();
    if (key == null)
    {
        return Results.Json(new MissingKeyError(), statusCode: 400);
    }
    var client = factory.CreateClient("gw2");
    var forward = "v2/" + path;
    if (ctx.Request.QueryString.HasValue)
        forward += ctx.Request.QueryString.Value;
    var req = new HttpRequestMessage(HttpMethod.Get, forward);
    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
    var resp = await client.SendAsync(req);
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, resp.Content.Headers.ContentType?.ToString() ?? "application/json", System.Text.Encoding.UTF8, (int)resp.StatusCode);
});

if (Environment.GetEnvironmentVariable("MCP_STDIO") == "1")
{
    using var scope = app.Services.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<ISecretStore>();
    var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    var server = new StdioServer(store, factory);
    await server.RunAsync();
}
else
{
    app.Run();
}

record ApiKeyDto(string Key);

record MissingKeyError
{
    public string Error => "MissingApiKey";
    public string Message => "Guild Wars 2 API key not configured";
    public string[] HowTo => new[] { "POST /api/apikey { key }", "or via MCP gw2.saveApiKey" };
}

public partial class Program { }
