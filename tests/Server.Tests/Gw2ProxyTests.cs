using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Server.Services;
using Xunit;

public class Gw2ProxyTests
{
    private readonly WebApplicationFactory<Program> _factory =
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ISecretStore, FakeStore>();
            });
        });

    private class FakeStore : ISecretStore
    {
        public Task DeleteApiKeyAsync() => Task.CompletedTask;
        public Task<string?> GetApiKeyAsync() => Task.FromResult<string?>(null);
        public Task<bool> HasApiKeyAsync() => Task.FromResult(false);
        public Task SaveApiKeyAsync(string key) => Task.CompletedTask;
    }

    [Fact]
    public async Task MissingApiKeyReturns400()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/gw2/account");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MissingApiKey", json.GetProperty("error").GetString());
    }
}
