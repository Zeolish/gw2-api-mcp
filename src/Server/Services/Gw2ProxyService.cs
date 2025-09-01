using System;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Server.Services;

public class Gw2ProxyService
{
    private readonly IHttpClientFactory _factory;
    private readonly ISecretStore _store;
    private readonly ILogger<Gw2ProxyService> _logger;

    public Gw2ProxyService(IHttpClientFactory factory, ISecretStore store, ILogger<Gw2ProxyService> logger)
    {
        _factory = factory;
        _store = store;
        _logger = logger;
    }

    public async Task<ProxyResponse> GetAsync(string path, string? query)
    {
        path = path.TrimStart('/');
        if (path.Contains("..")) throw new ArgumentException("Invalid path", nameof(path));
        if (!path.StartsWith("v2/"))
            path = "v2/" + path;

        if (!await _store.HasApiKeyAsync())
            throw new MissingApiKeyException();

        var key = await _store.GetApiKeyAsync();
        if (key == null)
            throw new MissingApiKeyException();

        var client = _factory.CreateClient("gw2");
        var url = path;
        if (!string.IsNullOrEmpty(query))
            url += query;

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var resp = await client.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        var ct = resp.Content.Headers.ContentType?.ToString() ?? "application/json";
        return new ProxyResponse(body, ct, resp.StatusCode);
    }
}

public record ProxyResponse(string Body, string ContentType, HttpStatusCode StatusCode);

