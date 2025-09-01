using System.Text.Json;
using Server.Mcp;
using Server.Services;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class McpTests
{
    [Fact]
    public async Task GetStatusWorks()
    {
        var server = new StdioServer(new DummyStore(), new Gw2ProxyService(new DummyFactory(), new DummyStore(), NullLogger<Gw2ProxyService>.Instance));
        var response = await server.InvokeAsync("{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"gw2.getStatus\",\"params\":{}}");
        var doc = JsonDocument.Parse(response);
        Assert.False(doc.RootElement.GetProperty("result").GetProperty("hasApiKey").GetBoolean());
    }

    private class DummyStore : ISecretStore
    {
        public Task DeleteApiKeyAsync() => Task.CompletedTask;
        public Task<string?> GetApiKeyAsync() => Task.FromResult<string?>(null);
        public Task<bool> HasApiKeyAsync() => Task.FromResult(false);
        public Task SaveApiKeyAsync(string key) => Task.CompletedTask;
    }

    private class DummyFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new HttpClientHandler());
    }
}
