using System.Text.Json;
using Server.Services;

namespace Server.Mcp;

public class StdioServer
{
    private readonly ISecretStore _store;
    private readonly IHttpClientFactory _factory;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public StdioServer(ISecretStore store, IHttpClientFactory factory)
    {
        _store = store;
        _factory = factory;
    }

    public async Task RunAsync(TextReader? input = null, TextWriter? output = null)
    {
        input ??= Console.In;
        output ??= Console.Out;
        string? line;
        while ((line = await input.ReadLineAsync()) != null)
        {
            try
            {
                var doc = JsonDocument.Parse(line);
                var id = doc.RootElement.GetProperty("id");
                var method = doc.RootElement.GetProperty("method").GetString()!;
                var result = await HandleAsync(method, doc.RootElement.GetProperty("params"));
                var resp = new { jsonrpc = "2.0", id, result };
                await output.WriteLineAsync(JsonSerializer.Serialize(resp, _json));
            }
            catch (RpcError ex)
            {
                var err = new { code = ex.Code, message = ex.Message, data = ex.Data };
                var resp = new { jsonrpc = "2.0", id = ex.Id, error = err };
                await output.WriteLineAsync(JsonSerializer.Serialize(resp, _json));
            }
        }
    }

    public async Task<string> InvokeAsync(string request)
    {
        using var reader = new StringReader(request + "\n");
        using var writer = new StringWriter();
        await RunAsync(reader, writer);
        return writer.ToString().Trim();
    }

    private async Task<object?> HandleAsync(string method, JsonElement @params)
    {
        switch (method)
        {
            case "gw2.getStatus":
                return new { server = Environment.MachineName, hasApiKey = await _store.HasApiKeyAsync() };
            case "gw2.hasApiKey":
                return new { hasApiKey = await _store.HasApiKeyAsync() };
            case "gw2.saveApiKey":
                var key = @params.GetProperty("key").GetString()!;
                await _store.SaveApiKeyAsync(key);
                return new { ok = true };
            case "gw2.deleteApiKey":
                await _store.DeleteApiKeyAsync();
                return new { ok = true };
            case "gw2.request":
                return await RequestAsync(@params.GetProperty("path").GetString()!,
                    @params.TryGetProperty("query", out var q) ? q.GetString() : null);
            case "gw2.account":
                return await RequestAsync("account", null);
            case "gw2.wallet":
                return await RequestAsync("wallet", null);
            case "gw2.bank":
                return await RequestAsync("bank", null);
            case "gw2.materials":
                return await RequestAsync("materials", null);
            case "gw2.characters":
                return await RequestAsync("characters", null);
            case "gw2.commerce.prices":
                return await RequestAsync("commerce/prices", null);
            default:
                throw new RpcError("Method not found", -32601, null, null);
        }
    }

    private async Task<JsonElement> RequestAsync(string path, string? query)
    {
        var key = await _store.GetApiKeyAsync();
        if (key == null)
            throw new RpcError("Missing API key", -32001, new MissingKeyError(), null);
        var client = _factory.CreateClient("gw2");
        var url = "v2/" + path.TrimStart('/');
        if (!string.IsNullOrEmpty(query))
            url += "?" + query;
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private class RpcError : Exception
    {
        public RpcError(string message, int code, object? data, JsonElement? id) : base(message)
        {
            Code = code;
            Data = data;
            Id = id;
        }
        public int Code { get; }
        public object? Data { get; }
        public JsonElement? Id { get; }
    }

    private record MissingKeyError
    {
        public string Error => "MissingApiKey";
        public string Message => "Guild Wars 2 API key not configured";
        public string[] HowTo => new[] { "POST /api/apikey { key }", "or via MCP gw2.saveApiKey" };
    }
}
