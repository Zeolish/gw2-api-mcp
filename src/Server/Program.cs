using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.Sqlite;
using Polly;
using Polly.Extensions.Http;
using Server.Mcp;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5123";
builder.WebHost.UseUrls($"http://localhost:{port}");

builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var appData = Path.Combine(AppContext.BaseDirectory, "AppData");
Directory.CreateDirectory(appData);
var connectionString = $"Data Source={Path.Combine(appData, "app.db")}";

builder.Services.AddScoped<IDbConnection>(_ =>
{
    var conn = new SqliteConnection(connectionString);
    conn.Open();
    return conn;
});

builder.Services.AddSingleton<ISecretStore, AesGcmSecretStore>();
builder.Services.AddTransient<Gw2ProxyService>();

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

app.UseExceptionHandler(a =>
{
    a.Run(async context =>
    {
        var error = context.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        if (error is MissingApiKeyException)
        {
            logger.LogWarning(error, "Missing API key");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "MissingApiKey",
                message = error.Message,
                howTo = new[] { "Open Web UI", "Paste key", "Save" }
            });
        }
        else
        {
            logger.LogError(error, "Unhandled exception");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "ServerError", message = "An unexpected error occurred" });
        }
    });
});

app.Use(async (ctx, next) =>
{
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("{method} {path}", ctx.Request.Method, ctx.Request.Path);
    await next();
});

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

app.MapControllers();

if (Environment.GetEnvironmentVariable("MCP_STDIO") == "1")
{
    using var scope = app.Services.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<ISecretStore>();
    var proxy = scope.ServiceProvider.GetRequiredService<Gw2ProxyService>();
    var server = new StdioServer(store, proxy);
    await server.RunAsync();
}
else
{
    app.Run();
}

public partial class Program { }

