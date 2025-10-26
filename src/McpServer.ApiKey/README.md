# MCP Server with API Key Authentication

This project implements an MCP (Model Context Protocol) server with API Key authentication.

## Features

- API Key-based authentication using custom middleware
- MCP HTTP transport support
- CORS enabled for browser access
- Three sample tools:
  - **MultiplicationTool**: Multiplies two numbers
  - **TemperatureConverterTool**: Converts between Celsius and Fahrenheit
  - **WeatherTools**: Gets weather alerts and forecasts from NWS API

## Configuration

The API key is configured in `appsettings.json` and `appsettings.Development.json`:

```json
{
  "ApiKey": "your-secret-api-key-here"
}
```

For development, the default API key is: `dev-api-key-12345`

## Authentication

All MCP endpoints require the `X-API-Key` header:

```http
X-API-Key: dev-api-key-12345
```

Endpoints that don't require authentication:
- `/status` - Health check endpoint
- `/health` - Health check endpoint

## Running the Server

```bash
dotnet run
```

The server will start on `https://localhost:5001` and `http://localhost:5000`

## Testing

Use the included `McpServer.http` file to test the endpoints with different scenarios:
- Health check without authentication
- MCP calls without API key (should fail with 401)
- MCP calls with valid API key

## Security Notes

- Store production API keys in Azure Key Vault or User Secrets
- Never commit real API keys to source control
- Rotate API keys regularly
- Use HTTPS in production
