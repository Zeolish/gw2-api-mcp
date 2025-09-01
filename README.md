# Guild Wars 2 API MCP

ASP.NET Core 8 backend with React/TypeScript frontend. Serves all Guild Wars 2 `/v2` API endpoints over HTTP and as an MCP stdio JSON-RPC server.

## Setup

```bash
cd src/WebUI
pnpm install
pnpm build
cd ../../

dotnet build ./src/Server -c Release
Dotnet test ./tests/Server.Tests -c Release
```

## HTTP server

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project ./src/Server
```

Endpoints:
- `GET /api/status`
- `GET /api/apikey`
- `POST /api/apikey { key }`
- `DELETE /api/apikey`
- `GET /api/gw2/*` → proxies to `/v2/*`

Swagger is available at `/swagger`. In production the compiled React app from `src/WebUI/dist` is served.

## MCP stdio

```bash
MCP_STDIO=1 dotnet run --project ./src/Server
```

Supported methods include `gw2.getStatus`, `gw2.hasApiKey`, `gw2.saveApiKey`, `gw2.deleteApiKey`, `gw2.request` and typed shims like `gw2.account`, `gw2.wallet`, `gw2.bank`, `gw2.materials`, `gw2.characters`, and `gw2.commerce.prices`.

## Adding endpoints

The HTTP proxy automatically forwards any path under `/api/gw2` to the official `/v2` API. To expose additional typed MCP methods, extend `StdioServer` with new cases.
