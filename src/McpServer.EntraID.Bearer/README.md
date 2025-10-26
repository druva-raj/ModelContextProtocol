# MCP Server with Microsoft EntraID Authentication

This MCP server **requires** Microsoft EntraID (Azure AD) authentication, supporting both:
- **Client Credentials Flow** (ClientID + Secret)
- **Bearer Token Authentication** (generated from ClientID + Secret)

## Features

- ✅ JWT Bearer token validation
- ✅ Microsoft EntraID integration
- ✅ Supports both v1.0 and v2.0 tokens
- ✅ **EntraID authentication is REQUIRED** - server will not start without proper configuration
- ✅ Multiple tools: Multiplication, Temperature Conversion, Weather

## Configuration

### 1. Azure Portal Setup

1. Register an application in [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** → **App registrations** → **New registration**
3. Note down:
   - **Application (client) ID**
   - **Directory (tenant) ID**
4. Create a client secret:
   - Go to **Certificates & secrets** → **New client secret**
   - Note down the secret value immediately
5. Configure API permissions (if needed)
6. Set the Audience (typically `api://[ClientId]` or custom)

### 2. Application Configuration

Update `appsettings.json` or use User Secrets:

```json
{
  "EntraID": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "Audience": "api://your-client-id",
    "Scopes": []
  },
  "ServerUrl": "http://localhost:5500/"
}
```

#### Using User Secrets (Recommended for Development)

```bash
cd src/McpServer.EntraID
dotnet user-secrets set "EntraID:TenantId" "your-tenant-id"
dotnet user-secrets set "EntraID:ClientId" "your-client-id"
dotnet user-secrets set "EntraID:ClientSecret" "your-client-secret"
dotnet user-secrets set "EntraID:Audience" "api://your-client-id"
```

## Running the Server

**Important:** The server requires EntraID configuration. If not configured, it will fail to start with an error message.

```bash
cd src/McpServer.EntraID
dotnet run
```

The server will start on `http://localhost:5500` by default.

If you see the error "FATAL ERROR: EntraID authentication is required but not configured!", ensure you have configured TenantId, ClientId, and Audience in appsettings.json or user secrets.

## Authentication Methods

### Method 1: Client Credentials Flow (Get Token First)

Use the ClientID and Secret to obtain a Bearer token:

```bash
# Get access token using client credentials
$tenantId = "your-tenant-id"
$clientId = "your-client-id"
$clientSecret = "your-client-secret"
$scope = "api://your-client-id/.default"

$body = @{
    client_id     = $clientId
    client_secret = $clientSecret
    scope         = $scope
    grant_type    = "client_credentials"
}

$response = Invoke-RestMethod -Method Post `
    -Uri "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" `
    -Body $body

$token = $response.access_token
Write-Host "Token: $token"
```

### Method 2: Use Bearer Token Directly

Once you have the token, use it in your requests:

```bash
# PowerShell
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

Invoke-RestMethod -Uri "http://localhost:5500/" -Headers $headers
```

```bash
# cURL
curl -H "Authorization: Bearer YOUR_TOKEN_HERE" http://localhost:5500/
```

## Testing the MCP Endpoints

### 1. Check Server Status

```bash
curl http://localhost:5500/
```

### 2. List Available Tools

```bash
curl -X POST http://localhost:5500/mcp \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/list",
    "id": 1
  }'
```

### 3. Call a Tool

```bash
curl -X POST http://localhost:5500/mcp \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "Multiply",
      "arguments": {
        "a": 6,
        "b": 7
      }
    },
    "id": 2
  }'
```

## Available Tools

1. **Multiply** - Multiply two numbers
2. **CelsiusToFahrenheit** - Convert Celsius to Fahrenheit
3. **FahrenheitToCelsius** - Convert Fahrenheit to Celsius
4. **GetAlerts** - Get weather alerts for a US state
5. **GetForecast** - Get weather forecast for a location (lat/long)

## Security Notes

- ✅ Always use HTTPS in production
- ✅ Store secrets in Azure Key Vault or User Secrets
- ✅ Never commit secrets to source control
- ✅ Configure appropriate CORS policies for production
- ✅ Validate token claims for authorization decisions

## Troubleshooting

### Token Validation Fails

- Ensure the `Audience` claim in the token matches configured audience
- Check that the token is not expired
- Verify the tenant ID is correct
- Ensure valid issuers include both v1.0 and v2.0 endpoints

### Authentication Not Working

- Check console logs for detailed error messages
- Verify EntraID configuration in appsettings.json
- Ensure the app registration has correct redirect URIs
- Check that the client secret hasn't expired

## References

- [Microsoft Identity Platform](https://docs.microsoft.com/azure/active-directory/develop/)
- [Client Credentials Flow](https://docs.microsoft.com/azure/active-directory/develop/v2-oauth2-client-creds-grant-flow)
- [JWT Token Validation](https://docs.microsoft.com/azure/active-directory/develop/access-tokens)
