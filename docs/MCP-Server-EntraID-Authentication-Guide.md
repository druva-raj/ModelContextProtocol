# MCP Server with Microsoft Entra ID Authentication

A comprehensive guide for securing your Model Context Protocol (MCP) server with Microsoft Entra ID (formerly Azure Active Directory) authentication and connecting it to clients like Azure AI Foundry.

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Part 1: Create an App Registration in Microsoft Entra ID](#part-1-create-an-app-registration-in-microsoft-entra-id)
4. [Part 2: Configure API Permissions and Scopes](#part-2-configure-api-permissions-and-scopes)
5. [Part 3: Create App Roles for Authorization](#part-3-create-app-roles-for-authorization)
6. [Part 4: Configure the MCP Server Application](#part-4-configure-the-mcp-server-application)
7. [Part 5: Deploy to Azure App Service](#part-5-deploy-to-azure-app-service)
8. [Part 6: Grant Access to Clients](#part-6-grant-access-to-clients)
9. [Part 7: Connect from Azure AI Foundry](#part-7-connect-from-azure-ai-foundry)
10. [Part 8: Connect from Other MCP Clients](#part-8-connect-from-other-mcp-clients)
11. [Troubleshooting](#troubleshooting)
12. [Reference: Key Concepts](#reference-key-concepts)

---

## Overview

This guide walks you through securing an MCP server with Microsoft Entra ID authentication. When complete, your MCP server will:

- Require valid JWT Bearer tokens for all MCP endpoints
- Validate tokens against your Microsoft Entra ID tenant
- Support connections from Azure AI Foundry managed identities
- Support connections from other authenticated clients

### Architecture Diagram

```
┌─────────────────────┐     1. Request Token      ┌──────────────────────┐
│  Azure AI Foundry   │ ────────────────────────► │  Microsoft Entra ID  │
│  (Managed Identity) │                           │     (OAuth 2.0)      │
└─────────────────────┘ ◄──────────────────────── └──────────────────────┘
         │                   2. JWT Token                    │
         │                                                   │
         │  3. Request + Bearer Token                        │
         ▼                                                   │
┌─────────────────────┐     4. Validate Token               │
│     MCP Server      │ ────────────────────────────────────┘
│  (Azure App Service)│
└─────────────────────┘
         │
         ▼
    5. Return MCP Response
```

---

## Prerequisites

Before starting, ensure you have:

### Tools
- **Azure CLI** installed and logged in (`az login`)
- **.NET 9 SDK** or later
- **Visual Studio Code** or similar editor

### Azure Resources
- An **Azure subscription** with permissions to create resources
- A **Microsoft Entra ID tenant** where you can create app registrations
- (Optional) An **Azure AI Foundry** project with system-assigned managed identity enabled

### Permissions Required
- **Application Developer** or **Application Administrator** role in Microsoft Entra ID
- **Contributor** role on the Azure subscription/resource group

---

## Part 1: Create an App Registration in Microsoft Entra ID

An **App Registration** represents your MCP server in Microsoft Entra ID. It defines who can access your application and how authentication works.

### Step 1.1: Create the App Registration

#### Using Azure Portal

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Microsoft Entra ID** → **App registrations**
3. Click **+ New registration**
4. Fill in the details:
   - **Name**: `spn-mcp-server-auth` (or your preferred name)
   - **Supported account types**: Select based on your needs:
     - *Single tenant*: Only users/apps in your organization
     - *Multitenant*: Users/apps from any organization
   - **Redirect URI**: Leave blank (not needed for API-only applications)
5. Click **Register**

#### Using Azure CLI

```powershell
# Create the app registration
az ad app create --display-name "spn-mcp-server-auth"

# Note the appId (client ID) from the output - you'll need this later
# Example output: "appId": "af6339ba-639d-4616-b959-8d9848a4faa3"
```

### Step 1.2: Note the Important IDs

After creation, note these values (found in the app registration's **Overview** page):

| Value | Description | Example |
|-------|-------------|---------|
| **Application (client) ID** | Unique identifier for your app | `af6339ba-639d-4616-b959-8d9848a4faa3` |
| **Directory (tenant) ID** | Your Microsoft Entra tenant | `978bbad2-037f-4859-8a78-385d36d264ee` |
| **Object ID** | Internal Entra object ID | `2702977a-012e-492e-b8a6-855ca0425ac9` |

### Step 1.3: Create a Service Principal

A **Service Principal** is the local representation of your app in the tenant. It's automatically created when you register through the portal, but may need to be created manually via CLI:

```powershell
# Create service principal for the app (if not already created)
az ad sp create --id af6339ba-639d-4616-b959-8d9848a4faa3
```

---

## Part 2: Configure API Permissions and Scopes

### Step 2.1: Set the Application ID URI (Identifier URI)

The **Application ID URI** is the unique identifier clients use when requesting tokens for your API. This becomes the **audience** claim in tokens.

#### Using Azure Portal

1. Go to your app registration
2. Navigate to **Expose an API**
3. Click **Set** next to "Application ID URI"
4. Choose a URI format:
   - **Option A** (Recommended for web apps): `https://your-app-name.azurewebsites.net`
   - **Option B** (API convention): `api://af6339ba-639d-4616-b959-8d9848a4faa3`
5. Click **Save**

#### Using Azure CLI

```powershell
# Set the identifier URI
az ad app update --id af6339ba-639d-4616-b959-8d9848a4faa3 `
    --identifier-uris "https://app-ext-eus2-mcp-profx-01.azurewebsites.net"
```

> ⚠️ **IMPORTANT**: The identifier URI you choose here **must match exactly** what clients use as the "audience" when connecting. This is a common source of authentication failures.

### Step 2.2: Create an OAuth2 Permission Scope

Scopes define what permissions clients can request. Create at least one scope for your MCP API.

#### Using Azure Portal

1. In **Expose an API**, click **+ Add a scope**
2. Fill in the details:
   - **Scope name**: `mcp.tools`
   - **Who can consent**: Admins and users (or Admins only for stricter control)
   - **Admin consent display name**: `Use MCP Tools`
   - **Admin consent description**: `Allows the application to execute MCP tools and functions`
   - **User consent display name**: `Use MCP Tools`
   - **User consent description**: `Executes MCP functions`
   - **State**: Enabled
3. Click **Add scope**

#### Using Azure CLI

```powershell
# First, get the current app manifest
$app = az ad app show --id af6339ba-639d-4616-b959-8d9848a4faa3 | ConvertFrom-Json

# Generate a unique GUID for the scope
$scopeId = [guid]::NewGuid().ToString()

# Create the scope (requires updating the app manifest)
# Note: This is easier to do through the portal
```

### Step 2.3: Preauthorize Trusted Client Applications (Optional)

Preauthorizing applications allows them to request tokens without requiring user consent each time.

#### Azure CLI Application ID

To allow Azure CLI to request tokens for testing:

```powershell
# Azure CLI's well-known application ID
$azureCliAppId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46"

# Get your scope ID from the app registration
$app = az ad app show --id af6339ba-639d-4616-b959-8d9848a4faa3 --query "api.oauth2PermissionScopes[0].id" -o tsv

# The preauthorization needs to be done via the portal or Graph API
```

#### Using Azure Portal

1. In **Expose an API**, scroll to **Authorized client applications**
2. Click **+ Add a client application**
3. Enter the client application ID (e.g., Azure CLI: `04b07795-8ddb-461a-bbee-02f9e1bf7b46`)
4. Check the scopes you want to authorize
5. Click **Add application**

---

## Part 3: Create App Roles for Authorization

**App Roles** provide role-based access control (RBAC). You can assign roles to users, groups, or service principals (like managed identities).

### Step 3.1: Create an App Role

#### Using Azure Portal

1. Go to your app registration
2. Navigate to **App roles**
3. Click **+ Create app role**
4. Fill in the details:
   - **Display name**: `MCP Access`
   - **Allowed member types**: Select based on your needs:
     - *Users/Groups*: For human users
     - *Applications*: For service principals/managed identities
     - *Both*: For maximum flexibility
   - **Value**: `MCP.Tools.All`
   - **Description**: `Full access to all MCP tools and functions`
   - **Enabled**: Yes
5. Click **Apply**

#### Using Azure CLI

```powershell
# Create app role via Graph API
# First, get the current app manifest
$appId = "af6339ba-639d-4616-b959-8d9848a4faa3"
$app = az ad app show --id $appId | ConvertFrom-Json

# Create a new role definition
$newRole = @{
    id = [guid]::NewGuid().ToString()
    allowedMemberTypes = @("User", "Application")
    displayName = "MCP Access"
    description = "Full access to all MCP tools and functions"
    isEnabled = $true
    value = "MCP.Tools.All"
}

# Add to existing roles (if any)
$roles = @($app.appRoles) + $newRole

# Update the app
$rolesJson = $roles | ConvertTo-Json -Compress
az ad app update --id $appId --app-roles $rolesJson
```

### Step 3.2: Verify the Role Was Created

```powershell
az ad app show --id af6339ba-639d-4616-b959-8d9848a4faa3 --query "appRoles" -o json
```

Expected output:
```json
[
  {
    "allowedMemberTypes": ["User", "Application"],
    "description": "Full access to all MCP tools and functions",
    "displayName": "MCP Access",
    "id": "b5f4dbb8-181b-4f41-a849-8ca0304796a6",
    "isEnabled": true,
    "value": "MCP.Tools.All"
  }
]
```

---

## Part 4: Configure the MCP Server Application

### Step 4.1: Install Required NuGet Packages

```powershell
cd your-mcp-server-project

# Core MCP packages
dotnet add package ModelContextProtocol.AspNetCore --prerelease

# Authentication packages
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.Identity.Web
```

### Step 4.2: Configure appsettings.json

Create or update your `appsettings.json` with the Entra ID configuration:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "ModelContextProtocol": "Debug"
    }
  },
  "AllowedHosts": "*",
  "ServerUrl": "https://your-app-name.azurewebsites.net",
  "EntraID": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "Audience": "https://your-app-name.azurewebsites.net"
  }
}
```

Replace the placeholders:
- `YOUR_TENANT_ID`: Your Microsoft Entra tenant ID (e.g., `978bbad2-037f-4859-8a78-385d36d264ee`)
- `YOUR_CLIENT_ID`: Your app registration's Application (client) ID (e.g., `af6339ba-639d-4616-b959-8d9848a4faa3`)
- `your-app-name`: Your Azure App Service name

### Step 4.3: Create the EntraID Options Model

Create a file `Models/EntraIDOptions.cs`:

```csharp
namespace YourNamespace.Models;

public class EntraIDOptions
{
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    
    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}/v2.0";
}
```

### Step 4.4: Configure Program.cs with JWT Bearer Authentication

Here's a complete `Program.cs` example:

```csharp
using System.Security.Claims;
using YourNamespace.Models;
using YourNamespace.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Load EntraID configuration
builder.Services.Configure<EntraIDOptions>(builder.Configuration.GetSection("EntraID"));
var entraIdOptions = builder.Configuration.GetSection("EntraID").Get<EntraIDOptions>()!;

Console.WriteLine($"EntraID authentication enabled for tenant: {entraIdOptions.TenantId}");

// Configure valid audiences and issuers
// Include multiple formats to handle different token versions
string[] validAudiences = new[] 
{ 
    entraIdOptions.Audience,  // e.g., https://your-app.azurewebsites.net
    entraIdOptions.ClientId   // e.g., af6339ba-639d-4616-b959-8d9848a4faa3
};

string[] validIssuers = new[]
{
    $"https://sts.windows.net/{entraIdOptions.TenantId}/",                    // v1 tokens
    $"{entraIdOptions.Instance.TrimEnd('/')}/{entraIdOptions.TenantId}/v2.0", // v2 tokens
    entraIdOptions.Authority
};

// Configure authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = entraIdOptions.Authority;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudiences = validAudiences,
        ValidIssuers = validIssuers,
        NameClaimType = "name",
        RoleClaimType = "roles",
    };
    
    // Optional: Add logging for debugging authentication issues
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var name = context.Principal?.Identity?.Name ?? "unknown";
            var oid = context.Principal?.FindFirstValue("oid") ?? "unknown";
            Console.WriteLine($"Token validated - Name: {name}, OID: {oid}");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        }
    };
})
.AddMcpAuthentication(); // Add MCP-specific authentication support

builder.Services.AddAuthorization();

// Configure MCP Server
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "Your MCP Server",
            Version = "1.0.0"
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(Program).Assembly);

// Enable CORS if needed
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Map MCP endpoint with authorization required
app.MapMcp("/mcp").RequireAuthorization();

// Health check endpoint (no auth required)
app.MapGet("/", () => new
{
    status = "running",
    server = "MCP Server with EntraID Authentication",
    authenticationEnabled = true,
    tenant = entraIdOptions.TenantId
}).AllowAnonymous();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.Run();
```

### Step 4.5: Create MCP Tools

Create your MCP tools using the `[McpServerToolType]` and `[McpServerTool]` attributes:

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace YourNamespace.Tools;

[McpServerToolType]
public class SampleTools
{
    [McpServerTool(Name = "hello_world")]
    [Description("Returns a greeting message")]
    public string HelloWorld(
        [Description("Name to greet")] string name)
    {
        return $"Hello, {name}! Welcome to the authenticated MCP server.";
    }
    
    [McpServerTool(Name = "add_numbers")]
    [Description("Adds two numbers together")]
    public int AddNumbers(
        [Description("First number")] int a,
        [Description("Second number")] int b)
    {
        return a + b;
    }
}
```

---

## Part 5: Deploy to Azure App Service

### Step 5.1: Create an App Service

#### Using Azure Portal

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **+ Create a resource** → **Web App**
3. Configure:
   - **Resource Group**: Create new or select existing
   - **Name**: `app-ext-eus2-mcp-profx-01` (must be globally unique)
   - **Runtime stack**: .NET 9 (or your version)
   - **Operating System**: Windows or Linux
   - **Region**: Choose your preferred region
   - **Pricing plan**: Basic (B1) or higher (required for Always On)
4. Click **Review + create** → **Create**

#### Using Azure CLI

```powershell
# Create resource group
az group create --name rg-ext-eus2-mcp-profx-01 --location eastus2

# Create App Service Plan (Basic tier for Always On support)
az appservice plan create `
    --name plan-ext-eus2-mcp-profx-01 `
    --resource-group rg-ext-eus2-mcp-profx-01 `
    --sku B1 `
    --is-linux

# Create Web App
az webapp create `
    --name app-ext-eus2-mcp-profx-01 `
    --resource-group rg-ext-eus2-mcp-profx-01 `
    --plan plan-ext-eus2-mcp-profx-01 `
    --runtime "DOTNET|9.0"
```

### Step 5.2: Enable Always On

**Always On** keeps your app warm and prevents cold start timeouts. This is important for MCP servers.

```powershell
az webapp config set `
    --name app-ext-eus2-mcp-profx-01 `
    --resource-group rg-ext-eus2-mcp-profx-01 `
    --always-on true
```

### Step 5.3: Configure App Settings

Set the EntraID configuration as environment variables:

```powershell
az webapp config appsettings set `
    --name app-ext-eus2-mcp-profx-01 `
    --resource-group rg-ext-eus2-mcp-profx-01 `
    --settings `
        EntraID__TenantId="978bbad2-037f-4859-8a78-385d36d264ee" `
        EntraID__ClientId="af6339ba-639d-4616-b959-8d9848a4faa3" `
        EntraID__Audience="https://app-ext-eus2-mcp-profx-01.azurewebsites.net" `
        EntraID__Instance="https://login.microsoftonline.com/" `
        ServerUrl="https://app-ext-eus2-mcp-profx-01.azurewebsites.net"
```

### Step 5.4: Deploy the Application

```powershell
# Navigate to your project directory
cd path/to/your/mcp-server

# Publish the application
dotnet publish -c Release -o ./publish

# Create a deployment zip
Compress-Archive -Path ./publish/* -DestinationPath ./deploy.zip -Force

# Deploy to Azure
az webapp deploy `
    --name app-ext-eus2-mcp-profx-01 `
    --resource-group rg-ext-eus2-mcp-profx-01 `
    --src-path ./deploy.zip `
    --type zip
```

### Step 5.5: Verify Deployment

```powershell
# Test the health endpoint
Invoke-WebRequest -Uri "https://app-ext-eus2-mcp-profx-01.azurewebsites.net/" -UseBasicParsing

# Test the MCP endpoint (should return 401 Unauthorized without a token)
Invoke-WebRequest -Uri "https://app-ext-eus2-mcp-profx-01.azurewebsites.net/mcp" -Method POST -UseBasicParsing
```

Expected: The root endpoint returns 200 OK, the MCP endpoint returns 401 Unauthorized.

---

## Part 6: Grant Access to Clients

For a client (like Azure AI Foundry's managed identity) to access your MCP server, you must assign them the app role.

### Step 6.1: Find the Client's Object ID

#### For Azure AI Foundry Managed Identity

1. Go to your Azure AI Foundry resource in the Azure Portal
2. Navigate to **Settings** → **Identity**
3. Ensure **System assigned** is set to **On**
4. Copy the **Object ID** (e.g., `44af7b32-23d9-43ad-b713-362457834e9d`)

#### Using Azure CLI

```powershell
# List service principals (search for your client)
az ad sp list --display-name "your-foundry-resource-name" --query "[].{name:displayName, objectId:id}" -o table
```

### Step 6.2: Get the App Role ID

```powershell
# Get the app role ID from your app registration
az ad app show --id af6339ba-639d-4616-b959-8d9848a4faa3 --query "appRoles[?value=='MCP.Tools.All'].id" -o tsv

# Example output: b5f4dbb8-181b-4f41-a849-8ca0304796a6
```

### Step 6.3: Get the Service Principal ID

```powershell
# Get the service principal ID for your app (not the app registration)
az ad sp show --id af6339ba-639d-4616-b959-8d9848a4faa3 --query "id" -o tsv

# Example output: 2702977a-012e-492e-b8a6-855ca0425ac9
```

### Step 6.4: Assign the Role

Use Microsoft Graph API to create the app role assignment:

```powershell
# Variables
$clientObjectId = "44af7b32-23d9-43ad-b713-362457834e9d"  # Foundry managed identity
$appRoleId = "b5f4dbb8-181b-4f41-a849-8ca0304796a6"       # MCP.Tools.All role
$resourceSpnId = "2702977a-012e-492e-b8a6-855ca0425ac9"   # MCP server service principal

# Create the role assignment using Graph API
az rest --method POST `
    --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$clientObjectId/appRoleAssignments" `
    --headers "Content-Type=application/json" `
    --body "{\"principalId\":\"$clientObjectId\",\"resourceId\":\"$resourceSpnId\",\"appRoleId\":\"$appRoleId\"}"
```

### Step 6.5: Verify the Assignment

```powershell
az rest --method GET `
    --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$clientObjectId/appRoleAssignments" `
    --query "value[?appRoleId=='$appRoleId']"
```

---

## Part 7: Connect from Azure AI Foundry

### Step 7.1: Enable Managed Identity on Foundry

1. Go to your Azure AI Foundry resource
2. Navigate to **Settings** → **Identity**
3. Set **System assigned** to **On**
4. Click **Save**
5. Note the **Object ID** that appears

### Step 7.2: Grant the Role (See Part 6)

Ensure you've completed [Part 6](#part-6-grant-access-to-clients) to assign the app role to the Foundry managed identity.

### Step 7.3: Add MCP Server Connection in Foundry

1. Open your Azure AI Foundry project
2. Navigate to **Settings** → **MCP Servers** (or similar)
3. Click **+ Add MCP Server**
4. Configure the connection:

| Setting | Value |
|---------|-------|
| **Name** | `my-mcp-server` (your choice) |
| **URL** | `https://app-ext-eus2-mcp-profx-01.azurewebsites.net/mcp` |
| **Authentication** | `Entra ID` |
| **Audience** | `https://app-ext-eus2-mcp-profx-01.azurewebsites.net` |

> ⚠️ **CRITICAL**: The **Audience** must match exactly the **Identifier URI** configured in your app registration. This is the most common cause of authentication failures.

### Step 7.4: Test the Connection

After adding the MCP server:

1. Click **Test Connection** or **Enumerate Tools**
2. If successful, you should see the list of tools from your MCP server
3. If you get a timeout error, check the [Troubleshooting](#troubleshooting) section

---

## Part 8: Connect from Other MCP Clients

### Using the C# MCP Client SDK

Here's a complete example of connecting to your MCP server from a C# application:

```csharp
using Azure.Identity;
using ModelContextProtocol.Client;

// Configuration
var mcpServerUrl = "https://app-ext-eus2-mcp-profx-01.azurewebsites.net/mcp";
var audience = "https://app-ext-eus2-mcp-profx-01.azurewebsites.net";
var tenantId = "978bbad2-037f-4859-8a78-385d36d264ee";

// Step 1: Acquire an access token
var credential = new AzureCliCredential(new AzureCliCredentialOptions
{
    TenantId = tenantId
});

var tokenResult = await credential.GetTokenAsync(
    new Azure.Core.TokenRequestContext([$"{audience}/.default"]),
    CancellationToken.None);

Console.WriteLine($"Token acquired, expires: {tokenResult.ExpiresOn}");

// Step 2: Create the MCP client with bearer token
var transportOptions = new HttpClientTransportOptions
{
    Endpoint = new Uri(mcpServerUrl),
    Name = "My MCP Client",
    AdditionalHeaders = new Dictionary<string, string>
    {
        { "Authorization", $"Bearer {tokenResult.Token}" }
    }
};

await using var client = await McpClient.CreateAsync(
    new HttpClientTransport(transportOptions),
    new McpClientOptions
    {
        ClientInfo = new()
        {
            Name = "My MCP Client",
            Version = "1.0.0"
        }
    });

Console.WriteLine($"Connected to: {client.ServerInfo?.Name}");

// Step 3: List and call tools
var tools = await client.ListToolsAsync();
Console.WriteLine($"Available tools: {tools.Count}");

foreach (var tool in tools)
{
    Console.WriteLine($"  - {tool.Name}: {tool.Description}");
}
```

### Required NuGet Packages

```powershell
dotnet add package Azure.Identity
dotnet add package ModelContextProtocol --prerelease
```

### Prerequisites for Azure CLI Credentials

1. Login to Azure CLI with the correct tenant:
   ```powershell
   az login --scope https://app-ext-eus2-mcp-profx-01.azurewebsites.net/mcp.tools
   ```

2. Ensure Azure CLI is preauthorized in your app registration (see [Part 2.3](#step-23-preauthorize-trusted-client-applications-optional))

---

## Troubleshooting

### Error: "The resource principal named api://... was not found"

**Cause**: The audience in the token request doesn't match the Identifier URI in your app registration.

**Solution**: 
1. Check your app registration's Identifier URI:
   ```powershell
   az ad app show --id YOUR_CLIENT_ID --query "identifierUris" -o json
   ```
2. Use the exact URI as the audience in your client configuration

### Error: "Token validation failed - Invalid audience"

**Cause**: The token's `aud` claim doesn't match what your server expects.

**Solution**:
1. Decode your token at [jwt.ms](https://jwt.ms)
2. Check the `aud` claim
3. Ensure your server's `ValidAudiences` includes this value
4. Update either the client's audience request or the server's valid audiences

### Error: "Timeout while enumerating tools"

**Cause**: The MCP server is not responding in time.

**Possible solutions**:
1. **Enable Always On** on your App Service (requires Basic tier or higher)
2. **Warm up the app** by making a request to the health endpoint before connecting
3. **Check the logs**:
   ```powershell
   az webapp log download --name YOUR_APP_NAME --resource-group YOUR_RG --log-file logs.zip
   ```

### Error: "401 Unauthorized"

**Cause**: The token is invalid, expired, or missing required claims.

**Solution**:
1. Verify the token is being sent:
   ```
   Authorization: Bearer eyJ0eXAi...
   ```
2. Check token claims at [jwt.ms](https://jwt.ms):
   - `aud` matches your Identifier URI
   - `iss` is from your tenant
   - Token is not expired (`exp` claim)
   - Required roles are present (`roles` claim)

### Error: "403 Forbidden"

**Cause**: The token is valid but lacks the required role/permission.

**Solution**:
1. Verify the app role assignment (see [Part 6](#part-6-grant-access-to-clients))
2. Check if your server is validating roles:
   ```csharp
   // In your authorization policy
   options.AddPolicy("McpAccess", policy =>
       policy.RequireClaim("roles", "MCP.Tools.All"));
   ```

### Error: "AADSTS65001 - User or administrator has not consented"

**Cause**: The client app needs consent to access your API.

**Solution**:
1. Preauthorize the client app (see [Part 2.3](#step-23-preauthorize-trusted-client-applications-optional))
2. Or grant admin consent:
   ```powershell
   az ad app permission admin-consent --id YOUR_CLIENT_APP_ID
   ```

### Viewing Server Logs

```powershell
# Download logs
az webapp log download `
    --name app-ext-eus2-mcp-profx-01 `
    --resource-group rg-ext-eus2-mcp-profx-01 `
    --log-file logs.zip

# Extract and view
Expand-Archive logs.zip -DestinationPath ./logs -Force
Get-Content ./logs/LogFiles/eventlog.xml -Tail 50
```

---

## Reference: Key Concepts

### What is an App Registration?

An **App Registration** is your application's identity in Microsoft Entra ID. It defines:
- **Who can access** your application (single tenant, multi-tenant)
- **What permissions** your application needs
- **What permissions** other applications can request from yours

### What is a Service Principal?

A **Service Principal** is the local instance of your app in a specific tenant. When you register an app:
- The **App Registration** is global (definition)
- The **Service Principal** is local (instance per tenant)

### What is an Identifier URI?

The **Identifier URI** (also called Application ID URI) uniquely identifies your API. Clients use this as the "resource" or "audience" when requesting tokens. Common formats:
- `api://YOUR_CLIENT_ID` - API convention
- `https://your-app.azurewebsites.net` - Website URL format

### What are App Roles?

**App Roles** are custom roles you define for your application. They appear as the `roles` claim in access tokens. Use them to control what different clients can do:
- `MCP.Tools.All` - Full access
- `MCP.Tools.Read` - Read-only access
- `MCP.Admin` - Administrative access

### What is a Managed Identity?

A **Managed Identity** is an automatically managed identity for Azure resources. Benefits:
- No credentials to manage
- Automatic rotation
- Works seamlessly with Azure services

Types:
- **System-assigned**: Tied to a single resource, deleted when resource is deleted
- **User-assigned**: Independent resource, can be shared across multiple resources

### OAuth 2.0 Flow for APIs

```
1. Client requests token from Microsoft Entra ID
   - Specifies the "scope" (resource + permission)
   - Example: https://your-app.azurewebsites.net/.default

2. Microsoft Entra ID validates the request
   - Checks if client is authorized
   - Checks if user consented (for delegated permissions)
   - Checks app role assignments (for application permissions)

3. Microsoft Entra ID issues JWT token
   - Contains claims: aud, iss, sub, roles, etc.

4. Client sends request to API with token
   - Authorization: Bearer <token>

5. API validates the token
   - Checks signature
   - Checks expiration
   - Checks issuer
   - Checks audience
   - Checks required claims/roles

6. API processes the request
```

---

## Summary Checklist

Before going live, verify:

- [ ] App Registration created with correct Identifier URI
- [ ] OAuth2 permission scope defined
- [ ] App role created for authorization
- [ ] MCP Server configured with correct EntraID settings
- [ ] App Service deployed with Always On enabled
- [ ] Client's managed identity granted the app role
- [ ] Client configured with correct URL and audience
- [ ] Connection tested and tools enumerated successfully

---

## Additional Resources

- [Microsoft Entra ID Documentation](https://learn.microsoft.com/en-us/entra/identity/)
- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- [MCP C# SDK GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
- [Azure App Service Documentation](https://learn.microsoft.com/en-us/azure/app-service/)
- [JWT Token Debugger](https://jwt.ms)
